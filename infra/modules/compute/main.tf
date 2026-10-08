terraform {
  required_providers {
    google-beta = {
      source = "hashicorp/google-beta"
    }
  }
}

resource "google_cloud_run_v2_service" "converter_web" {
  provider    = google-beta
  name        = "md-converter-${var.environment}"
  location    = var.region
  project     = var.project_id
  ingress     = "INGRESS_TRAFFIC_ALL"
  iap_enabled = length(var.iap_authorized_domains) > 0 ? true : false

  template {
    service_account  = var.converter_sa_email
    session_affinity = true # Enforces Session Affinity (sticky sessions) for Blazor Server SignalR/WebSockets

    scaling {
      min_instance_count = var.min_instances
      max_instance_count = var.max_instances
    }

    containers {
      image = var.container_image

      resources {
        limits = {
          cpu    = "1"
          memory = "1Gi"
        }
      }

      ports {
        container_port = 8080
      }

      env {
        name  = "GCP_PROJECT_ID"
        value = var.project_id
      }

      env {
        name  = "GCS_TEMP_BUCKET"
        value = var.bucket_name
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }

      startup_probe {
        initial_delay_seconds = 5
        timeout_seconds       = 3
        period_seconds        = 10
        failure_threshold     = 3
        http_get {
          path = "/api/health"
          port = 8080
        }
      }

      liveness_probe {
        initial_delay_seconds = 15
        timeout_seconds       = 3
        period_seconds        = 20
        failure_threshold     = 3
        http_get {
          path = "/api/health"
          port = 8080
        }
      }
    }
  }
}

data "google_project" "current" {
  project_id = var.project_id
}

# IAP access: only authorized members reach converter_web when IAP is enabled
resource "google_iap_web_cloud_run_service_iam_binding" "iap_access" {
  count                  = length(var.iap_authorized_domains) > 0 ? 1 : 0
  project                = var.project_id
  location               = google_cloud_run_v2_service.converter_web.location
  cloud_run_service_name = google_cloud_run_v2_service.converter_web.name
  role                   = "roles/iap.httpsResourceAccessor"
  members                = var.iap_authorized_domains
}

# IAP's service agent needs run.invoker to forward authenticated requests to the service.
resource "google_cloud_run_v2_service_iam_member" "iap_invoker" {
  count    = length(var.iap_authorized_domains) > 0 ? 1 : 0
  project  = var.project_id
  location = google_cloud_run_v2_service.converter_web.location
  name     = google_cloud_run_v2_service.converter_web.name
  role     = "roles/run.invoker"
  member   = "serviceAccount:service-${data.google_project.current.number}@gcp-sa-iap.iam.gserviceaccount.com"
}

# Grant public invocation permission only if IAP is disabled and allow_unauthenticated is true
resource "google_cloud_run_v2_service_iam_member" "public_access" {
  count    = length(var.iap_authorized_domains) == 0 && var.allow_unauthenticated ? 1 : 0
  project  = var.project_id
  location = var.region
  name     = google_cloud_run_v2_service.converter_web.name
  role     = "roles/run.invoker"
  member   = "allUsers"
}
