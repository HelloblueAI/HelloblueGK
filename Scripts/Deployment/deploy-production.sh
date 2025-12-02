#!/bin/bash

# Production deployment script for HelloblueGK Aerospace Engine API
# Enterprise-grade deployment with security, monitoring, and scalability

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging function
log() {
    echo -e "${BLUE}[$(date +'%Y-%m-%d %H:%M:%S')]${NC} $1"
}

error() {
    echo -e "${RED}[ERROR]${NC} $1"
    exit 1
}

success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

# Check prerequisites
check_prerequisites() {
    log "Checking prerequisites..."
    
    command -v docker >/dev/null 2>&1 || error "Docker is required but not installed"
    command -v kubectl >/dev/null 2>&1 || error "kubectl is required but not installed"
    command -v helm >/dev/null 2>&1 || error "Helm is required but not installed"
    
    # Check if kubectl is configured
    kubectl cluster-info >/dev/null 2>&1 || error "kubectl is not configured or cluster is not accessible"
    
    success "All prerequisites met"
}

# Generate secure secrets
generate_secrets() {
    log "Generating secure secrets..."
    
    # Generate random secrets
    export DB_PASSWORD=$(openssl rand -base64 32)
    export GRAFANA_PASSWORD=$(openssl rand -base64 32)
    export JWT_SECRET=$(openssl rand -base64 64)
    export REDIS_PASSWORD=$(openssl rand -base64 32)
    
    # Create Kubernetes secrets
    kubectl create secret generic hellobluegk-secrets \
        --from-literal=database-password="$DB_PASSWORD" \
        --from-literal=grafana-password="$GRAFANA_PASSWORD" \
        --from-literal=jwt-secret="$JWT_SECRET" \
        --from-literal=redis-password="$REDIS_PASSWORD" \
        --from-literal=database-connection="Server=postgres;Database=hellobluegk;Username=aerospace;Password=$DB_PASSWORD" \
        --dry-run=client -o yaml | kubectl apply -f -
    
    success "Secrets generated and applied"
}

# Build and push Docker image
build_image() {
    log "Building Docker image..."
    
    # Build the image
    docker build -t hellobluegk:latest .
    
    # Tag for registry (if using external registry)
    if [ ! -z "$REGISTRY_URL" ]; then
        docker tag hellobluegk:latest "$REGISTRY_URL/hellobluegk:latest"
        docker push "$REGISTRY_URL/hellobluegk:latest"
        success "Image pushed to registry"
    else
        success "Image built successfully"
    fi
}

# Deploy to Kubernetes
deploy_kubernetes() {
    log "Deploying to Kubernetes..."
    
    # Apply namespace
    kubectl create namespace hellobluegk --dry-run=client -o yaml | kubectl apply -f -
    
    # Apply all Kubernetes resources
    kubectl apply -f k8s-deployment.yaml -n hellobluegk
    
    # Wait for deployment to be ready
    log "Waiting for deployment to be ready..."
    kubectl rollout status deployment/hellobluegk-api -n hellobluegk --timeout=300s
    
    success "Kubernetes deployment completed"
}

# Setup monitoring stack
setup_monitoring() {
    log "Setting up monitoring stack..."
    
    # Add Prometheus Helm repository
    helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
    helm repo update
    
    # Install Prometheus Stack
    helm install prometheus prometheus-community/kube-prometheus-stack \
        --namespace monitoring \
        --create-namespace \
        --set grafana.enabled=true \
        --set prometheus.prometheusSpec.serviceMonitorSelectorNilUsesHelmValues=false \
        --set prometheus.prometheusSpec.podMonitorSelectorNilUsesHelmValues=false \
        --set prometheus.prometheusSpec.ruleSelectorNilUsesHelmValues=false \
        --wait
    
    success "Monitoring stack deployed"
}

# Setup ingress controller
setup_ingress() {
    log "Setting up ingress controller..."
    
    # Add NGINX Ingress Helm repository
    helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
    helm repo update
    
    # Install NGINX Ingress Controller
    helm install ingress-nginx ingress-nginx/ingress-nginx \
        --namespace ingress-nginx \
        --create-namespace \
        --set controller.service.type=LoadBalancer \
        --wait
    
    success "Ingress controller deployed"
}

# Setup cert-manager for SSL certificates
setup_cert_manager() {
    log "Setting up cert-manager for SSL certificates..."
    
    # Install cert-manager
    kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.0/cert-manager.yaml
    
    # Wait for cert-manager to be ready
    kubectl wait --for=condition=ready pod -l app.kubernetes.io/instance=cert-manager -n cert-manager --timeout=300s
    
    # Create ClusterIssuer for Let's Encrypt
    cat <<EOF | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: admin@helloblue.com
    privateKeySecretRef:
      name: letsencrypt-prod
    solvers:
    - http01:
        ingress:
          class: nginx
EOF
    
    success "Cert-manager configured"
}

# Run health checks
health_checks() {
    log "Running health checks..."
    
    # Check if pods are running
    kubectl get pods -n hellobluegk -l app=hellobluegk-api
    
    # Check service endpoints
    kubectl get endpoints -n hellobluegk
    
    # Test API health endpoint
    SERVICE_IP=$(kubectl get service hellobluegk-service -n hellobluegk -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
    
    if [ ! -z "$SERVICE_IP" ]; then
        log "Testing API health endpoint..."
        curl -f "http://$SERVICE_IP/health" || warning "Health check failed"
    else
        warning "Service IP not available yet"
    fi
    
    success "Health checks completed"
}

# Security hardening
security_hardening() {
    log "Applying security hardening..."
    
    # Apply Pod Security Standards
    kubectl label namespace hellobluegk pod-security.kubernetes.io/enforce=restricted
    kubectl label namespace hellobluegk pod-security.kubernetes.io/audit=restricted
    kubectl label namespace hellobluegk pod-security.kubernetes.io/warn=restricted
    
    # Create Network Policies
    cat <<EOF | kubectl apply -f -
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: hellobluegk-network-policy
  namespace: hellobluegk
spec:
  podSelector:
    matchLabels:
      app: hellobluegk-api
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          name: ingress-nginx
    ports:
    - protocol: TCP
      port: 8080
  egress:
  - to:
    - namespaceSelector:
        matchLabels:
          name: monitoring
    ports:
    - protocol: TCP
      port: 9090
  - to: []
    ports:
    - protocol: TCP
      port: 53
    - protocol: UDP
      port: 53
EOF
    
    success "Security hardening applied"
}

# Performance optimization
performance_optimization() {
    log "Applying performance optimizations..."
    
    # Configure resource quotas
    cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: ResourceQuota
metadata:
  name: hellobluegk-quota
  namespace: hellobluegk
spec:
  hard:
    requests.cpu: "32"
    requests.memory: 64Gi
    limits.cpu: "64"
    limits.memory: 128Gi
    persistentvolumeclaims: "10"
EOF
    
    # Configure HPA
    kubectl apply -f k8s-deployment.yaml -n hellobluegk
    
    success "Performance optimizations applied"
}

# Main deployment function
main() {
    log "🚀 Starting HelloblueGK Aerospace Engine API Production Deployment..."
    
    # Check prerequisites
    check_prerequisites
    
    # Generate secrets
    generate_secrets
    
    # Build and push image
    build_image
    
    # Setup infrastructure
    setup_ingress
    setup_cert_manager
    setup_monitoring
    
    # Deploy application
    deploy_kubernetes
    
    # Apply security and performance optimizations
    security_hardening
    performance_optimization
    
    # Run health checks
    health_checks
    
    # Display deployment information
    log "🎉 Production deployment completed successfully!"
    log ""
    log "📊 Deployment Information:"
    log "   - API Endpoint: https://api.helloblue.com"
    log "   - Grafana Dashboard: http://localhost:3000"
    log "   - Prometheus: http://localhost:9090"
    log "   - Kubernetes Dashboard: kubectl proxy"
    log ""
    log "🔧 Useful Commands:"
    log "   - View logs: kubectl logs -f deployment/hellobluegk-api -n hellobluegk"
    log "   - Scale deployment: kubectl scale deployment hellobluegk-api --replicas=5 -n hellobluegk"
    log "   - Update deployment: kubectl rollout restart deployment/hellobluegk-api -n hellobluegk"
    log ""
    log "🔒 Security Features:"
    log "   - SSL/TLS encryption enabled"
    log "   - JWT authentication configured"
    log "   - Network policies applied"
    log "   - Pod security standards enforced"
    log ""
    log "📈 Monitoring Features:"
    log "   - Prometheus metrics collection"
    log "   - Grafana dashboards"
    log "   - Horizontal Pod Autoscaling"
    log "   - Health checks and probes"
}

# Run main function
main "$@" 