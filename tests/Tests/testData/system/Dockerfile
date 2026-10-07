# This Dockerfile is never built. It exists only as a Renovate-trackable version pin for the
# Testcontainers images used in System tests (see mu88.Shared.Testing's TestcontainerImages helper and
# https://github.com/testcontainers/testcontainers-dotnet/discussions/1782 for the pattern this follows).
FROM postgres:18-alpine AS postgres
FROM mcr.microsoft.com/playwright:v1.63.0-noble AS playwright
FROM sheyenrath/wiremock.net-alpine:2.14.0 AS wiremock
