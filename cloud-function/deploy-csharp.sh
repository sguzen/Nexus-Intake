#!/bin/bash

# Exit immediately if a command exits with a non-zero status
set -e

PROJECT_ID="nexus-intake"
REGION="us-central1"
SERVICE_NAME="nexus-intake-api"
IMAGE="gcr.io/$PROJECT_ID/$SERVICE_NAME"

echo "[SYSTEM] Checking for local secrets file..."
if [ ! -f ".env.production" ]; then
    echo "[CRITICAL ERROR] .env.production file not found!"
    echo "Create a .env.production file in this folder to hold your deployment keys."
    exit 1
fi

# Load the secrets securely into the terminal session
source .env.production

echo "[SYSTEM] Submitting Docker build to Google Cloud..."
# Note: The '..' points to the root directory where your Dockerfile lives
gcloud builds submit --tag $IMAGE --project=$PROJECT_ID ..

echo "[SYSTEM] Deploying C# API to Cloud Run..."
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
  --set-env-vars="GoogleCloud__BucketName=$PROD_BUCKET_NAME,Telegram__BotToken=$PROD_TELEGRAM_TOKEN,CloudFunction__Url=$PROD_FUNCTION_URL"

echo ""
echo "[SYSTEM: DEPLOYMENT COMPLETE ✓]"
echo "Your C# API is now live and your keys are secure."