"""Bounded special-frame source transport; C# alone compiles policy semantics."""
import copy
import json
from pathlib import Path

SOURCE = Path('data/combat/presentation/special_frame_bindings.json')
MAXIMUM_SOURCE_BYTES = 1024 * 1024


def validate(document):
    if not isinstance(document, dict) or set(document) != {'schemaVersion', 'actions'} or type(document['schemaVersion']) is not int or document['schemaVersion'] != 1:
        raise ValueError('Expected complete special-frame source transport schema1')
    actions = document['actions']
    if not isinstance(actions, list) or not 1 <= len(actions) <= 256:
        raise ValueError('Special frame records require a bounded complete list')
    allowed = {'actionId', 'formId', 'kind', 'variant', 'family', 'mode', 'motionId', 'policyVersion', 'mediaBindingVersion', 'timeline'}
    required = {'actionId', 'formId', 'kind', 'variant', 'family', 'mode', 'motionId'}
    for action in actions:
        if not isinstance(action, dict) or not required <= set(action) or set(action) - allowed:
            raise ValueError('A typed whole special frame source is required')
        for field in ('actionId', 'formId', 'motionId'):
            if not isinstance(action[field], str) or not action[field].strip():
                raise ValueError('Stable typed action/form/media identities are required')
        for field in ('kind', 'variant', 'family', 'mode'):
            if type(action[field]) is not int:
                raise ValueError('Typed special mode selectors must be integer transport values')
        for field in ('policyVersion', 'mediaBindingVersion'):
            if action.get(field) is not None and not isinstance(action[field], str):
                raise ValueError('Captured source versions must be strings when present')
        # Preserve marker/frame contents without implementing another policy,
        # marker dependency resolver, projection or temporal numeric compiler.
        if action.get('timeline') is not None and not isinstance(action['timeline'], dict):
            raise ValueError('Local frame timeline must be an object when present')
    return document


def load(repository):
    with (Path(repository) / SOURCE).open('rb') as file:
        raw = file.read(MAXIMUM_SOURCE_BYTES + 1)
    if len(raw) > MAXIMUM_SOURCE_BYTES:
        raise ValueError('Special frame source exceeds the allowed file size')
    return validate(json.loads(raw.decode('utf-8-sig')))


def apply(catalog, document):
    result = copy.deepcopy(catalog)
    result['specialFrames'] = copy.deepcopy(validate(document))
    return result
