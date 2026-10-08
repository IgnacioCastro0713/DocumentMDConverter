variable "project_id" {
  description = "The GCP project ID"
  type        = string
}

variable "region" {
  description = "The GCP region for the registry"
  type        = string
}

variable "environment" {
  description = "The deployment environment (dev, prod)"
  type        = string
}
