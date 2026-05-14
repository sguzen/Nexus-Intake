# Nexus Intake — Cloud Function Deployment
# Prerequisites: gcloud CLI installed and authenticated

PROJECT_ID="nexus-intake"
REGION="us-central1"
FUNCTION_NAME="nexus-intake-extractor"
ENTRY_POINT="extract_document"
RUNTIME="python311"
MEMORY="256MB"
TIMEOUT="60s"
MAX_INSTANCES="3"
MIN_INSTANCES="0"

gcloud functions deploy $FUNCTION_NAME \
  --project=$PROJECT_ID \
  --region=$REGION \
  --runtime=$RUNTIME \
  --entry-point=$ENTRY_POINT \
  --trigger-http \
  --allow-unauthenticated \
  --memory=$MEMORY \
  --timeout=$TIMEOUT \
  --max-instances=$MAX_INSTANCES \
  --min-instances=$MIN_INSTANCES \
  --set-env-vars="GCP_PROJECT=$PROJECT_ID,GCP_LOCATION=$REGION" \
  --source=.

echo ""
echo "Cloud Function deployed. URL:"
gcloud functions describe $FUNCTION_NAME --project=$PROJECT_ID --region=$REGION --format="value(httpsTrigger.url)"
