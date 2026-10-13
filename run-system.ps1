Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "Starting Airport Management System..."
Write-Host "Existing Docker volumes are kept. Database and ticket files persist between runs."

docker compose up -d --build mysql rabbitmq airport-api airport-ui ticket-worker

Write-Host ""
Write-Host "System is starting."
Write-Host "Customer UI: http://localhost:5173/customer-login.html"
Write-Host "Manager UI:  http://localhost:5173/admin-login.html"
Write-Host "Swagger:     http://localhost:8080/swagger"
Write-Host ""
Write-Host "To stop containers without deleting data, run: docker compose stop"
