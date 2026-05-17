# Nexus Intake — Cloud Function Deployment
# Prerequisites: gcloud CLI installed and authenticated

PROJECT_ID="project-52b64cdc-d513-41a6-bbd"
REGION="us-central1"
FUNCTION_NAME="nexus-intake-extractor"
ENTRY_POINT="extract_document"
RUNTIME="python311"
MEMORY="1024MB"
TIMEOUT="60s"
MAX_INSTANCES="3"
MIN_INSTANCES="0"

echo "Deploying $FUNCTION_NAME to $PROJECT_ID ($REGION)..."
echo ""

gcloud functions deploy $FUNCTION_NAME \
  --project=$PROJECT_ID \
  --region=$REGION \
  --runtime=$RUNTIME \
  --entry-point=$ENTRY_POINT \
  --trigger-http \
  --no-allow-unauthenticated \
  --memory=$MEMORY \
  --timeout=$TIMEOUT \
  --max-instances=$MAX_INSTANCES \
  --min-instances=$MIN_INSTANCES \
  --set-env-vars="GCP_PROJECT=$PROJECT_ID,GCP_LOCATION=$REGION" \
  --source=.

DEPLOY_EXIT=$?

echo ""
if [ $DEPLOY_EXIT -eq 0 ]; then
  echo "Cloud Function deployed. URL:"
  gcloud functions describe $FUNCTION_NAME --project=$PROJECT_ID --region=$REGION --format="value(httpsTrigger.url)" 2>/dev/null || echo "(could not fetch URL — check console)"
else
  echo "Deploy FAILED with exit code $DEPLOY_EXIT. Check errors above."
fi

echo ""
read -p "Press Enter to exit..."
