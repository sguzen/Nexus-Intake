# Nexus Intake — Cloud Run Deployment
# Build and deploy the .NET API to Google Cloud Run

PROJECT_ID="nexus-intake"
REGION="us-central1"
SERVICE_NAME="nexus-intake-api"
IMAGE="gcr.io/$PROJECT_ID/$SERVICE_NAME"

gcloud builds submit --tag $IMAGE ..

gcloud run deploy $SERVICE_NAME \
  --project=$PROJECT_ID \
  --region=$REGION \
  --image=$IMAGE \
  --platform=managed \
  --allow-unauthenticated \
  --memory=512Mi \
  --cpu=1 \
  --max-instances=3 \
  --min-instances=0 \
  --timeout=120 \
  --set-env-vars="Gcs__BucketName=nexus-intake-raw"

echo ""
echo "Cloud Run service deployed. URL:"
gcloud run services describe $SERVICE_NAME --project=$PROJECT_ID --region=$REGION --format="value(status.url)"
