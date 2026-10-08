terraform {
  required_version = ">= 1.5.0"
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = ">= 5.0"
    }
    random = {
      source  = "hashicorp/random"
      version = ">= 3.0"
    }
  }
  # backend "gcs" {
  #   bucket = "YOUR_TF_STATE_BUCKET_PROD"
  #   prefix = "md-converter/prod"
  # }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

locals {
  environment = "prod"
}

# 1. IAM Module - Creates dedicated Service Account and grants least-privilege Roles
module "iam" {
  source      = "../../modules/iam"
  project_id  = var.project_id
  environment = local.environment
}

# 2. Storage Module - Sets up temporary GCS bucket with 1-day lifecycle
module "storage" {
  source             = "../../modules/storage"
  project_id         = var.project_id
  region             = var.region
  environment        = local.environment
  converter_sa_email = module.iam.converter_sa_email
  bucket_prefix      = var.bucket_prefix
}

# 3. Artifact Registry Module - Provisions Docker repository for container images
module "artifact_registry" {
  source      = "../../modules/artifact_registry"
  project_id  = var.project_id
  region      = var.region
  environment = local.environment
}

# 4. Compute Module - Deploys serverless Cloud Run Service with Session Affinity for Blazor SignalR
module "compute" {
  source                = "../../modules/compute"
  project_id            = var.project_id
  region                = var.region
  environment           = local.environment
  container_image       = var.container_image
  converter_sa_email    = module.iam.converter_sa_email
  bucket_name           = module.storage.bucket_name
  min_instances         = var.min_instances
  max_instances         = var.max_instances
  allow_unauthenticated = var.allow_unauthenticated

  depends_on = [module.storage, module.iam]
}
