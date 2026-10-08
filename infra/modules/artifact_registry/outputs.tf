output "repository_id" {
  description = "The repository ID"
  value       = google_artifact_registry_repository.docker_repo.repository_id
}

output "repository_name" {
  description = "The fully qualified repository resource name"
  value       = google_artifact_registry_repository.docker_repo.name
}

output "image_base_url" {
  description = "The base image URL to tag and push containers"
  value       = "${var.region}-docker.pkg.dev/${var.project_id}/${google_artifact_registry_repository.docker_repo.repository_id}/app"
}
