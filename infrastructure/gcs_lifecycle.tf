# GCS Lifecycle Rule — Self-Destruct Uploaded ID Documents after 24 Hours
# Place this in your Terraform configuration or apply via gcloud CLI.
# This ensures compliance with TRNC privacy laws by guaranteeing data deletion
even if the C# application crashes before reaching the delete step.

google_storage_bucket_lifecycle_rule:
  action:
    type: "Delete"
  condition:
    age: 1 # days
    matchesPrefix: "intakes/"

# Alternative: gcloud command to apply via CLI
# gcloud storage buckets add-iam-policy-binding gs://YOUR_BUCKET_NAME \
#   --lifecycle-file=lifecycle.json
