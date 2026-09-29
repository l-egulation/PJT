# syntax=docker/dockerfile:1

FROM eclipse-temurin:21-jdk AS build
WORKDIR /workspace
COPY gradlew gradlew.bat ./
COPY gradle gradle
COPY settings.gradle.kts build.gradle.kts gradle.properties ./
ENV GRADLE_USER_HOME=/opt/gradle
RUN sed -i 's/\r$//' gradlew && chmod +x gradlew && ./gradlew --no-daemon --version
COPY apps/market-worker apps/market-worker
RUN --mount=type=cache,id=hanjjak-gradle-caches,target=/opt/gradle/caches \
    --mount=type=cache,id=hanjjak-market-worker-project-gradle,target=/workspace/.gradle \
    ./gradlew --no-daemon :apps:market-worker:bootJar

FROM eclipse-temurin:21-jre
RUN useradd --system --uid 10001 --no-create-home app
COPY --from=build --chown=app:app /workspace/apps/market-worker/build/libs/market-worker.jar /app.jar
USER app
ENTRYPOINT ["java", "-jar", "/app.jar"]
