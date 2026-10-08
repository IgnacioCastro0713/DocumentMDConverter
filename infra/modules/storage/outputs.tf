output "bucket_name" {
  description = "The name of the temporary Cloud Storage bucket"
  value       = google_storage_bucket.temp_bucket.name
}

output "bucket_url" {
  description = "The URL of the temporary Cloud Storage bucket"
  value       = google_storage_bucket.temp_bucket.url
}
