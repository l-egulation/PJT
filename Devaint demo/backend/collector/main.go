package main

import (
	"context"
	"errors"
	"flag"
	"fmt"
	"log/slog"
	"net/http"
	"os"
	"os/signal"
	"strconv"
	"strings"
	"syscall"
	"time"
)

func env(name, fallback string) string {
	if s := os.Getenv(name); s != "" {
		return s
	}
	return fallback
}
func positive(name string, fallback int64) (int64, error) {
	s := os.Getenv(name)
	if s == "" {
		return fallback, nil
	}
	n, err := strconv.ParseInt(s, 10, 64)
	if err != nil || n <= 0 {
		return 0, fmt.Errorf("invalid %s", name)
	}
	return n, nil
}
func run() error {
	health := flag.Bool("healthcheck", false, "check the local API and exit")
	flag.Parse()
	if *health {
		client := &http.Client{Timeout: 2 * time.Second}
		response, err := client.Get("http://127.0.0.1:8080/healthz")
		if err != nil {
			return err
		}
		defer response.Body.Close()
		if response.StatusCode != 200 {
			return errors.New("unhealthy")
		}
		return nil
	}
	token := os.Getenv("ADMIN_TOKEN")
	if file := os.Getenv("ADMIN_TOKEN_FILE"); file != "" {
		if token != "" {
			return errors.New("set only ADMIN_TOKEN or ADMIN_TOKEN_FILE")
		}
		b, err := os.ReadFile(file)
		if err != nil {
			return errors.New("cannot read ADMIN_TOKEN_FILE")
		}
		token = strings.TrimSpace(string(b))
	}
	if len(token) < 24 {
		return errors.New("configure ADMIN_TOKEN or ADMIN_TOKEN_FILE with at least 24 characters")
	}
	quota, err := positive("MAX_STORAGE_BYTES", 2<<30)
	if err != nil {
		return err
	}
	daily, err := positive("PLAYER_DAILY_BYTES", 64<<20)
	if err != nil {
		return err
	}
	days, err := positive("RETENTION_DAYS", 30)
	if err != nil || days > 365 {
		return errors.New("RETENTION_DAYS must be 1..365")
	}
	store, err := openStore(env("DATA_DIR", "./data"), quota, daily, time.Duration(days)*24*time.Hour)
	if err != nil {
		return err
	}
	defer store.close()
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()
	if err = store.cleanup(ctx, time.Now()); err != nil {
		return err
	}
	api := newServer(store, token, strings.Split(os.Getenv("CORS_ORIGINS"), ","))
	server := &http.Server{Addr: env("HTTP_ADDR", ":8080"), Handler: api.handler(), ReadHeaderTimeout: 5 * time.Second, ReadTimeout: 30 * time.Second, WriteTimeout: 30 * time.Second, IdleTimeout: 60 * time.Second, MaxHeaderBytes: 16384}
	go func() {
		ticker := time.NewTicker(time.Hour)
		defer ticker.Stop()
		for {
			select {
			case <-ctx.Done():
				return
			case now := <-ticker.C:
				if err := store.cleanup(ctx, now); err != nil {
					slog.Error("cleanup failed", "error_type", fmt.Sprintf("%T", err))
				}
			}
		}
	}()
	done := make(chan error, 1)
	go func() { done <- server.ListenAndServe() }()
	slog.Info("collector listening", "address", server.Addr)
	select {
	case err = <-done:
		if !errors.Is(err, http.ErrServerClosed) {
			return err
		}
	case <-ctx.Done():
		shutdown, cancel := context.WithTimeout(context.Background(), 10*time.Second)
		defer cancel()
		return server.Shutdown(shutdown)
	}
	return nil
}
func main() {
	if err := run(); err != nil {
		slog.Error("collector stopped", "reason", err.Error())
		os.Exit(1)
	}
}
