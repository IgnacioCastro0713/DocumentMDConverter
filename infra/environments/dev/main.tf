terraform {
  required_version = ">= 1.5.0"
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = ">= 5.0"
    }
    google-beta = {
      source  = "hashicorp/google-beta"
      version = ">= 5.0"
    }
    random = {
      source  = "hashicorp/random"
      version = ">= 3.0"
    }
  }
  backend "gcs" {
    bucket = "document-md-converter-state"
    prefix = "dev/state"
  }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

provider "google-beta" {
  project = var.project_id
  region  = var.region
}

locals {
  environment = "dev"
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


# 4. Compute Module - Deploys serverless Cloud Run Service with Session Affinity for Blazor SignalR
module "compute" {
  source                = "../../modules/compute"
  project_id            = var.project_id
  region                = var.region
  environment           = local.environment
  container_image       = var.container_image
  converter_sa_email    = module.iam.converter_sa_email
  bucket_name           = module.storage.bucket_name
  min_instances          = var.min_instances
  max_instances          = var.max_instances
  allow_unauthenticated  = var.allow_unauthenticated
  iap_authorized_domains = var.iap_authorized_domains

  depends_on = [module.storage, module.iam]
}
