
# 🚀 Nexus Intake

**AI-Powered Telegram Extraction Bot for TRNC Documents**

Nexus Intake is a highly efficient, serverless pipeline designed to extract structured data from Turkish Republic of Northern Cyprus (TRNC) ID cards and vehicle insurance policies. Users interact with a Telegram bot by sending photos of documents, which are processed via Google Cloud Storage and Vertex AI (Gemini 1.5 Flash), returning precisely formatted data in a cyberpunk-themed terminal UI.

## 🏗 Architecture

The system is split into a fast, cost-effective C# API handling the webhooks and a Python Cloud Function handling the heavy AI inference.

1.  **Telegram Client:** User sends a photo to the bot.
2.  **C# .NET 8 Web API:** Receives the webhook, parses the Telegram payload, and uploads the image to Google Cloud Storage.
3.  **Python Cloud Function:** Triggered via HTTP from the C# app. Downloads the image from GCS, passes it to **Vertex AI (Gemini 1.5 Flash-001)** with a strict JSON schema.
4.  **Response:** The C# API formats the extracted JSON into Telegram MarkdownV2 (`CyberTerminalFormatter`) and replies to the user.

---

## 🛠 Prerequisites

* [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
* [Google Cloud CLI (`gcloud`)](https://cloud.google.com/sdk/docs/install)
* A Telegram Bot Token (from [@BotFather](https://t.me/BotFather))
* [Ngrok](https://ngrok.com/) (for local testing)

---

## 🚀 Local Development Setup

### 1. Google Cloud Configuration

1. Create a Google Cloud Project (e.g., `nexus-intake`).
2. Enable the Vertex AI API:
   ```bash
   gcloud services enable aiplatform.googleapis.com
   ```
3. Create a globally unique Cloud Storage bucket (e.g., `nexus-intake-raw-yourinitials`).
4. Grant your local environment Application Default Credentials (ADC):
   ```bash
   gcloud auth application-default login
   ```

### 2. Deploy the AI Extraction Engine (Python)

Navigate to the `cloud-function` directory and deploy the function to Google Cloud:

```bash
cd cloud-function
bash deploy.sh
```

Note the HTTP Trigger URL that is output at the end of the deployment.

### 3. Configure the C# Webhook API

Navigate to the `src/NexusIntake.Api` directory.

Open `appsettings.json` and populate your specific configuration:

```json
{
  "GoogleCloud": {
    "BucketName": "YOUR_UNIQUE_BUCKET_NAME"
  },
  "Telegram": {
    "BotToken": "YOUR_TELEGRAM_BOT_TOKEN"
  },
  "CloudFunction": {
    "Url": "YOUR_PYTHON_FUNCTION_URL"
  }
}
```

### 4. Run the Local Tunnel

Start the .NET application, forcing it to listen on the unencrypted HTTP port:

```bash
dotnet run --urls="http://localhost:5000"
```

In a separate terminal, start Ngrok to expose port 5000 to the internet:

```bash
ngrok http 5000
```

Register your Ngrok URL with Telegram:

```bash
curl -X POST "https://api.telegram.org/bot<YOUR_BOT_TOKEN>/setWebhook?url=<YOUR_NGROK_URL>/webhook/telegram"
```

---

## ☁️ Production Deployment (Cloud Run)

To deploy the C# API to a fully serverless, scale-to-zero Cloud Run environment:

1. Open `cloud-function/deploy-csharp.sh`.
2. Ensure the `--set-env-vars` flag contains your live environment variables:
   ```bash
   --set-env-vars="GoogleCloud__BucketName=[...],Telegram__BotToken=[...],CloudFunction__Url=[...]"
   ```
3. Execute the deployment script:
   ```bash
   bash deploy-csharp.sh
   ```
4. Update your Telegram Webhook to point to the new Cloud Run URL provided by the deployment script.

---

## 🔒 Security & IAM

This project utilizes Google Cloud's **Application Default Credentials (ADC)** and **Default Compute Service Accounts** to adhere to strict organization policies prohibiting the creation of static JSON keys.

Ensure the default compute service account has the following IAM role to invoke the Gemini API:

```
roles/aiplatform.user
```
