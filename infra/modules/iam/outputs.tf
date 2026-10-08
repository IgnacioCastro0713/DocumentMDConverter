output "converter_sa_email" {
  description = "The email address of the converter Service Account"
  value       = google_service_account.converter_sa.email
}

output "converter_sa_id" {
  description = "The ID of the converter Service Account"
  value       = google_service_account.converter_sa.id
}
