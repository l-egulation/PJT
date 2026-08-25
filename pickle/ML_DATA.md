# Fashion Image Model

The closet image analyzer uses the Kaggle Fashion Product Images (Small)
dataset. Kaggle reports its license as MIT. The download command records the
source and download time in `pickle_backend/data/fashion/DATASET_INFO.json`.
Dataset files and trained models are local artifacts and are excluded from Git
and Docker build contexts.

## Train

Run the stack first, then download the dataset and train the model:

```bash
docker compose up -d --build
docker compose exec backend python manage.py download_fashion_dataset
docker compose exec backend python manage.py train_fashion_classifier --limit 8000
```

The trained model is saved to
`pickle_backend/ml_models/fashion_image_classifier.joblib`. Use `--limit 0`
to train with every matching image; this takes substantially longer.

## Use

The clothes registration screen sends a selected image to:

```text
POST /api/v1/closet/analyze-image/
Content-Type: multipart/form-data
field: image
```

The response contains `category`, `season`, `color`, confidence values, and
model metadata. These values prefill the registration form and remain editable.

This model classifies visual attributes for registration. Outfit ranking is a
separate hybrid recommender trained from `RecommendationEvent` feedback.

## Outfit recommendation

Recommendation requires TPO, live weather, and one preferred style. Preferred
colors are optional. Candidate outfits receive transparent condition scores:

- season match: +5
- preferred-style color match: +4
- preferred-style material match: +2
- optional preferred-color match: +3
- formal TPO match: +2

The rule score is combined with the Random Forest preference probability. A
saved outfit or a rating of 4 or 5 is treated as positive feedback. The model
features include TPO, weather, selected style, style match ratio, optional color
match ratio, and each item's category attributes.

After changing the recommendation feature schema, rebuild its demo training
data and model:

```bash
docker compose exec backend python manage.py seed_ml_data --reset
docker compose exec backend python manage.py train_recommender
```
