"""Optional LLM reranking for already-generated outfit candidates."""

import json
import logging

from django.conf import settings
from openai import OpenAIError

logger = logging.getLogger(__name__)


SYSTEM_PROMPT = (
    'You are a fashion coordination assistant. '
    'You must choose ONLY from the provided outfit candidates. '
    'Do not create new clothing items. '
    'Do not change item IDs. '
    'Do not change candidate IDs. '
    'Return strict JSON only. '
    'If no candidate is suitable, still choose the best available candidate. '
    'Reasons must be written in Korean. '
    'Reasons should be concise and user-friendly. '
    'Do not mention internal score numbers unless explicitly requested.'
)


def _client():
    if not settings.GMS_API_KEY:
        return None
    from openai import OpenAI
    return OpenAI(base_url=settings.GMS_OPENAI_BASE_URL, api_key=settings.GMS_API_KEY)


def _item_payload(item):
    return {
        'id': item.id,
        'category': item.category,
        'name': item.name,
        'color': item.color,
        'season': item.season,
        'style': item.style or item.aihub_style,
        'material': item.material,
    }


def _candidate_payload(candidate):
    return {
        'candidate_id': candidate['candidate_id'],
        'rule_score': round(float(candidate.get('rule_score') or 0), 3),
        'ml_score': (
            round(float(candidate['ml_probability']) * 100, 3)
            if candidate.get('ml_probability') is not None
            else None
        ),
        'feedback_score': round(float(candidate.get('feedback_bonus') or 0), 3),
        'final_score': round(float(candidate.get('final_score') or candidate.get('rule_score') or 0), 3),
        'items': [_item_payload(item) for item in candidate.get('items', [])],
    }


def build_rerank_prompt(user, candidates, request_context):
    """Build the minimal JSON prompt payload for candidate-only reranking."""
    payload = {
        'user_condition': {
            'tpo': request_context.get('tpo'),
            'preferred_style': request_context.get('preferred_style'),
            'preferred_colors': request_context.get('preferred_colors') or [],
            'weather': request_context.get('weather') or {},
        },
        'candidates': [_candidate_payload(candidate) for candidate in candidates],
        'response_schema': {
            'selected_candidate_id': 'candidate_id from candidates',
            'reason_summary': 'short Korean summary',
            'reasons': ['max 3 concise Korean reasons'],
            'style_keywords': ['max 5 Korean style keywords'],
        },
    }
    return json.dumps(payload, ensure_ascii=False)


def call_llm_reranker(prompt):
    """Call the GMS OpenAI-compatible chat API for strict JSON reranking."""
    client = _client()
    if client is None:
        raise RuntimeError('GMS_API_KEY is not configured.')

    response = client.chat.completions.create(
        model=settings.GMS_VISION_MODEL,
        response_format={'type': 'json_object'},
        max_tokens=320,
        timeout=settings.LLM_RERANK_TIMEOUT,
        messages=[
            {'role': 'system', 'content': SYSTEM_PROMPT},
            {'role': 'user', 'content': prompt},
        ],
    )
    return response.choices[0].message.content


def parse_llm_rerank_response(response_text):
    """Parse strict JSON returned by the LLM."""
    if not response_text:
        raise ValueError('Empty LLM rerank response.')
    payload = json.loads(response_text)
    if not isinstance(payload, dict):
        raise ValueError('LLM rerank response must be a JSON object.')
    return payload


def apply_llm_rerank_result(candidates, llm_result):
    """Validate the LLM choice and return a safe selected candidate + metadata."""
    candidate_by_id = {candidate['candidate_id']: candidate for candidate in candidates}
    selected_id = llm_result.get('selected_candidate_id')
    selected = candidate_by_id.get(selected_id) or candidates[0]
    fallback = selected_id not in candidate_by_id

    reasons = llm_result.get('reasons')
    if not isinstance(reasons, list):
        reasons = []
    reasons = [str(reason).strip() for reason in reasons if str(reason).strip()][:3]

    reason_summary = str(llm_result.get('reason_summary') or '').strip()
    if not reason_summary:
        reason_summary = '조건에 가장 잘 맞는 코디를 골랐어요.'

    style_keywords = llm_result.get('style_keywords')
    if not isinstance(style_keywords, list):
        style_keywords = []
    style_keywords = [str(keyword).strip() for keyword in style_keywords if str(keyword).strip()][:5]

    return selected, {
        'llm_reranked': not fallback,
        'llm_selected_candidate_id': selected.get('candidate_id'),
        'llm_reason_summary': reason_summary,
        'llm_reasons': reasons,
        'style_keywords': style_keywords,
        'llm_fallback': fallback,
    }


def rerank_outfit_candidates(user, candidates, request_context):
    """Optionally ask an LLM to pick one of the top scored candidates."""
    if len(candidates) <= 1:
        logger.info('LLM reranker skipped: candidate_count=%s', len(candidates))
        return candidates[0], {'llm_reranked': False, 'llm_fallback': False}

    logger.info('LLM reranker enabled: candidate_count=%s', len(candidates))
    try:
        prompt = build_rerank_prompt(user, candidates, request_context)
        response_text = call_llm_reranker(prompt)
        llm_result = parse_llm_rerank_response(response_text)
        selected, meta = apply_llm_rerank_result(candidates, llm_result)
        logger.info(
            'LLM reranker selected candidate_id=%s fallback=%s',
            meta.get('llm_selected_candidate_id'),
            meta.get('llm_fallback'),
        )
        return selected, meta
    except (OpenAIError, json.JSONDecodeError, KeyError, IndexError, ValueError, TypeError, RuntimeError) as exc:
        logger.warning('LLM reranker failed; falling back to rule/ML top candidate: %s', exc)
        return candidates[0], {'llm_reranked': False, 'llm_fallback': True}
