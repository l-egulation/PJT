FROM golang:1.27.3-alpine AS build
WORKDIR /src
COPY go.mod go.sum ./
RUN go mod download
COPY *.go schema.sql ./
RUN CGO_ENABLED=0 go build -trimpath -ldflags="-s -w" -o /collector . && mkdir /data

FROM gcr.io/distroless/static-debian12:nonroot
COPY --from=build /collector /collector
COPY --from=build --chown=65532:65532 /data /data
ENV DATA_DIR=/data HTTP_ADDR=:8080 GOMEMLIMIT=384MiB
USER 65532:65532
EXPOSE 8080
VOLUME ["/data"]
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 CMD ["/collector", "-healthcheck"]
ENTRYPOINT ["/collector"]
