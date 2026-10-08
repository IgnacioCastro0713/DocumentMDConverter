variable "project_id" {
  description = "The GCP project ID"
  type        = string
}

variable "region" {
  description = "The GCP region for the storage bucket"
  type        = string
}

variable "environment" {
  description = "The deployment environment (dev, prod)"
  type        = string
}

variable "converter_sa_email" {
  description = "Email of the converter service account to grant storage permissions"
  type        = string
}

variable "bucket_prefix" {
  description = "Globally unique bucket name prefix; the environment is appended"
  type        = string
  default     = "document-md-converter-temp"
}
