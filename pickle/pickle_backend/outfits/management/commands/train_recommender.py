from pathlib import Path

import joblib
from django.conf import settings
from django.core.exceptions import ObjectDoesNotExist
from django.core.management.base import BaseCommand, CommandError
from django.utils import timezone
from sklearn.ensemble import RandomForestClassifier
from sklearn.feature_extraction import DictVectorizer
from sklearn.metrics import f1_score
from sklearn.model_selection import GroupKFold, cross_val_predict
from sklearn.pipeline import Pipeline

from outfits.models import RecommendationEvent


POSITIVE_FEEDBACK_KEYWORDS = (
    'good', 'great', 'comfortable', 'nice', 'love',
    '\uc88b', '\ud3b8\ud574', '\ub9cc\uc871', '\uc608\uc058', '\uac00\ubcbc',
    '\ub530\ub73b', '\uc2dc\uc6d0', '\uc798 \ub9de',
)
NEGATIVE_FEEDBACK_KEYWORDS = (
    'bad', 'hot', 'cold', 'uncomfortable', 'heavy',
    '\ub365', '\ucd94', '\ubd88\ud3b8', '\ubb34\uac81', '\ubcc4\ub85c',
    '\uc544\uc26c', '\uc548 \ub9de',
)


def feedback_adjustment(feedback):
    text = (feedback or '').lower()
    if not text:
        return 0
    score = 0
    if any(keyword in text for keyword in POSITIVE_FEEDBACK_KEYWORDS):
        score += 0.4
    if any(keyword in text for keyword in NEGATIVE_FEEDBACK_KEYWORDS):
        score -= 0.6
    return score


def evaluation_score(event):
    evaluation = None
    if event.outfit_id:
        try:
            evaluation = event.outfit.evaluation
        except ObjectDoesNotExist:
            evaluation = None
    if evaluation:
        score = (
            evaluation.rating * 0.40
            + evaluation.fit_score * 0.20
            + evaluation.activity_score * 0.25
            + evaluation.satisfaction_score * 0.15
        )
        return score + feedback_adjustment(evaluation.feedback)
    if event.rating is not None:
        return float(event.rating)
    return 4.0 if event.saved else 2.0


class Command(BaseCommand):
    help = 'Train and persist the outfit recommendation Random Forest model.'

    def add_arguments(self, parser):
        parser.add_argument('--output', default=str(settings.BASE_DIR / 'ml_models' / 'outfit_recommender.joblib'))

    def handle(self, *args, **options):
        events = list(RecommendationEvent.objects.exclude(features={}).order_by('id'))
        labels = [int(evaluation_score(event) >= 3.6) for event in events]
        groups = [event.user_id for event in events]
        if len(events) < 20 or len(set(labels)) < 2:
            raise CommandError('At least 20 events containing both positive and negative labels are required.')

        pipeline = Pipeline([
            ('vectorizer', DictVectorizer(sparse=True)),
            ('model', RandomForestClassifier(
                n_estimators=300,
                min_samples_leaf=2,
                class_weight='balanced',
                random_state=42,
                n_jobs=-1,
            )),
        ])

        unique_groups = set(groups)
        cv_f1 = None
        if len(unique_groups) >= 2:
            cv = GroupKFold(n_splits=min(5, len(unique_groups)))
            predictions = cross_val_predict(
                pipeline,
                [event.features for event in events],
                labels,
                groups=groups,
                cv=cv,
            )
            cv_f1 = f1_score(labels, predictions)

        pipeline.fit([event.features for event in events], labels)
        output = Path(options['output'])
        output.parent.mkdir(parents=True, exist_ok=True)
        joblib.dump({
            'pipeline': pipeline,
            'trained_at': timezone.now().isoformat(),
            'sample_count': len(events),
            'group_kfold_f1': cv_f1,
        }, output)
        score_text = f'{cv_f1:.3f}' if cv_f1 is not None else 'not available'
        self.stdout.write(self.style.SUCCESS(
            f'Saved model to {output} (samples={len(events)}, GroupKFold F1={score_text})'
        ))
