
build:
	dotnet build

.PHONY: test
test:
	dotnet test --logger "console;verbosity=detailed"

build-image:
	docker build -t altinntjenesterregistry.azurecr.io/altinn-apps-monitor:latest -f src/Altinn.Apps.Monitoring/Dockerfile .

run-image: build-image
	docker run --rm altinntjenesterregistry.azurecr.io/altinn-apps-monitor:latest

scan-image:
	trivy image altinntjenesterregistry.azurecr.io/altinn-apps-monitor:latest

dive-image:
	dive altinntjenesterregistry.azurecr.io/altinn-apps-monitor:latest

acr-login:
	az acr login --name altinntjenesterregistry

push-image: build-image
	docker push altinntjenesterregistry.azurecr.io/altinn-apps-monitor:latest

build-manifests: push-image
	kubectl kustomize infra/deployment/at24 > infra/deployment/at24/result.yaml

deploy-manifests: build-manifests
	kubectl apply -k infra/deployment/at24

flux-push:
    cd infra/deployment/
	flux push artifact oci://altinntjenesterregistry.azurecr.io/configs/altinn-apps-monitor:$(git rev-parse --short HEAD) \
		--provider=generic \
		--reproducible \
		--path="." \
		--source="$(git config --get remote.origin.url)" \
		--revision="$(git branch --show-current)/$(git rev-parse HEAD)" || echo "Failed to push artifact" >&2
	flux tag artifact oci://altinntjenesterregistry.azurecr.io/configs/altinn-apps-monitor:$(git rev-parse --short HEAD) \
		--provider=generic \
		--tag at24 || echo "Failed to tag artifact" >&2
