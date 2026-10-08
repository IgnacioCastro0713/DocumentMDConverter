variable "project_id" {
  description = "The GCP project ID"
  type        = string
}

variable "region" {
  description = "The GCP region for Cloud Run"
  type        = string
}

variable "environment" {
  description = "The deployment environment (dev, prod)"
  type        = string
}

variable "container_image" {
  description = "Full container image URL in Artifact Registry"
  type        = string
  default     = "us-docker.pkg.dev/cloudrun/container/hello"
}

variable "converter_sa_email" {
  description = "Service account email for Cloud Run service"
  type        = string
}

variable "bucket_name" {
  description = "Name of the temporary storage bucket"
  type        = string
}

variable "min_instances" {
  description = "Minimum number of container instances (0 for scale-to-zero)"
  type        = number
  default     = 0
}

variable "max_instances" {
  description = "Maximum number of container instances for DDoS protection and cost capping"
  type        = number
  default     = 2
}

variable "allow_unauthenticated" {
  description = "Whether to allow public access"
  type        = bool
  default     = true
}

variable "iap_authorized_domains" {
  description = "Accounts/domains authorized to access via IAP (e.g. user:user@example.com)"
  type        = list(string)
  default     = []
}
