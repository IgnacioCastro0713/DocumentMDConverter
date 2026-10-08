resource "google_service_account" "converter_sa" {
  account_id   = "sa-converter-${var.environment}"
  display_name = "DocumentMDConverter Service Account (${var.environment})"
}

# Grant Service Usage Consumer (required to consume Cloud Vision API under serverless ADC)
resource "google_project_iam_member" "converter_service_usage" {
  project = var.project_id
  role    = "roles/serviceusage.serviceUsageConsumer"
  member  = "serviceAccount:${google_service_account.converter_sa.email}"
}

# Grant BigQuery Job User to Converter SA (for job tracking and audit ingestion)
resource "google_project_iam_member" "converter_bigquery" {
  project = var.project_id
  role    = "roles/bigquery.jobUser"
  member  = "serviceAccount:${google_service_account.converter_sa.email}"
}

# Grant Service Account Token Creator to Converter SA (required to self-sign GCS URLs without local JSON keys)
resource "google_project_iam_member" "converter_token_creator" {
  project = var.project_id
  role    = "roles/iam.serviceAccountTokenCreator"
  member  = "serviceAccount:${google_service_account.converter_sa.email}"
}
