# build docker image
cd Trading && docker build -t trading-api:latest -f "src/Trading.API/Dockerfile" .