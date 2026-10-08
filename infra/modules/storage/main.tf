resource "google_storage_bucket" "temp_bucket" {
  name          = "${var.bucket_prefix}-${var.environment}"
  project       = var.project_id
  location      = var.region
  force_destroy = true

  uniform_bucket_level_access = true

  # Disable soft delete policy to prevent GCS from retaining temporary files for 7 days
  soft_delete_policy {
    retention_duration_seconds = 0
  }

  # Enable CORS for direct uploads and browser downloads
  cors {
    origin          = ["*"]
    method          = ["GET", "HEAD", "PUT", "POST", "OPTIONS"]
    response_header = ["*"]
    max_age_seconds = 3600
  }

  # Autoclean temporary files older than 7 days to enforce 7-day retention policy
  lifecycle_rule {
    condition {
      age = 7
    }
    action {
      type = "Delete"
    }
  }
}

# Grant objectAdmin to Converter SA
resource "google_storage_bucket_iam_member" "sa_storage_admin" {
  bucket = google_storage_bucket.temp_bucket.name
  role   = "roles/storage.objectAdmin"
  member = "serviceAccount:${var.converter_sa_email}"
}
