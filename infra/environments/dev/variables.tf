variable "project_id" {
  description = "The Google Cloud Project ID"
  type        = string
}

variable "region" {
  description = "The GCP region for all resources"
  type        = string
  default     = "us-central1"
}

variable "container_image" {
  description = "The full image URI to deploy to Cloud Run"
  type        = string
  default     = "us-docker.pkg.dev/cloudrun/container/hello"
}

variable "min_instances" {
  description = "Minimum number of Cloud Run instances (0 enables true scale-to-zero)"
  type        = number
  default     = 0
}

variable "max_instances" {
  description = "Maximum number of instances for billing protection"
  type        = number
  default     = 2
}

variable "allow_unauthenticated" {
  description = "Whether to allow unauthenticated public web access"
  type        = bool
  default     = false
}

variable "iap_authorized_domains" {
  description = "Accounts/domains authorized to access via IAP (roles/iap.httpsResourceAccessor)"
  type        = list(string)
  default     = []
}

variable "bucket_prefix" {
  description = "Globally unique bucket name prefix; the environment is appended"
  type        = string
  default     = "document-md-converter-temp"
}
