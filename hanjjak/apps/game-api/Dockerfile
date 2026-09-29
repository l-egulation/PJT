# syntax=docker/dockerfile:1

FROM eclipse-temurin:21-jdk AS build
WORKDIR /workspace
COPY gradlew gradlew.bat ./
COPY gradle gradle
COPY settings.gradle.kts build.gradle.kts gradle.properties ./
ENV GRADLE_USER_HOME=/opt/gradle
RUN sed -i 's/\r$//' gradlew && chmod +x gradlew && ./gradlew --no-daemon --version
COPY apps/game-api apps/game-api
COPY modules modules
COPY packages/sim-core packages/sim-core
COPY packages/game-content/versions packages/game-content/versions
RUN --mount=type=cache,id=hanjjak-gradle-caches,target=/opt/gradle/caches \
    --mount=type=cache,id=hanjjak-game-api-project-gradle,target=/workspace/.gradle \
    ./gradlew --no-daemon :apps:game-api:bootJar

FROM eclipse-temurin:21-jre
RUN useradd --system --uid 10001 --no-create-home app
COPY --from=build --chown=app:app /workspace/apps/game-api/build/libs/game-api.jar /app.jar
COPY apps/game-api/docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod 0555 /docker-entrypoint.sh
USER app
EXPOSE 8080
ENTRYPOINT ["/docker-entrypoint.sh"]
