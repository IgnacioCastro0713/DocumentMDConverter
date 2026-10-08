resource "google_artifact_registry_repository" "docker_repo" {
  project       = var.project_id
  location      = var.region
  repository_id = "document-md-converter-${var.environment}"
  description   = "Dedicated Docker container repository for DocumentMDConverter (${var.environment})"
  format        = "DOCKER"
}
