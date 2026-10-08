output "web_url" {
  description = "The public web URL of your DocumentMDConverter instance (PROD)"
  value       = module.compute.web_url
}

output "artifact_registry_image_base" {
  description = "The base URL for pushing Docker images to Artifact Registry"
  value       = module.artifact_registry.image_base_url
}

output "storage_bucket_name" {
  description = "The temporary Cloud Storage bucket name"
  value       = module.storage.bucket_name
}

output "service_account_email" {
  description = "The Service Account email used by Cloud Run"
  value       = module.iam.converter_sa_email
}
