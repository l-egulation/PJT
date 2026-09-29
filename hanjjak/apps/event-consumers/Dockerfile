# syntax=docker/dockerfile:1

FROM eclipse-temurin:21-jdk AS build
WORKDIR /workspace
COPY gradlew gradlew.bat ./
COPY gradle gradle
COPY settings.gradle.kts build.gradle.kts gradle.properties ./
ENV GRADLE_USER_HOME=/opt/gradle
RUN sed -i 's/\r$//' gradlew && chmod +x gradlew && ./gradlew --no-daemon --version
COPY modules/events modules/events
COPY apps/event-consumers apps/event-consumers
RUN --mount=type=cache,id=hanjjak-gradle-caches,target=/opt/gradle/caches \
    --mount=type=cache,id=hanjjak-event-consumers-project-gradle,target=/workspace/.gradle \
    ./gradlew --no-daemon :apps:event-consumers:bootJar

FROM eclipse-temurin:21-jre
RUN useradd --system --uid 10001 --no-create-home app
COPY --from=build --chown=app:app /workspace/apps/event-consumers/build/libs/event-consumers.jar /app.jar
USER app
ENTRYPOINT ["java", "-jar", "/app.jar"]
