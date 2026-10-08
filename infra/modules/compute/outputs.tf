output "web_url" {
  description = "The public web URL of the DocumentMDConverter Cloud Run service"
  value       = google_cloud_run_v2_service.converter_web.uri
}

output "service_name" {
  description = "The name of the Cloud Run service"
  value       = google_cloud_run_v2_service.converter_web.name
}
