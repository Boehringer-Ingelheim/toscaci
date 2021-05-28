package main

import (
	"context"
	_ "embed"
	log "github.com/sirupsen/logrus"
	"os"
	"os/signal"
	"toscactl/cli"
)

func main() {
	ctx, stop :=signal.NotifyContext(context.Background(),os.Interrupt)
	defer stop()
	go func() {
		select {
		case <-ctx.Done():
			log.Info("Program interruption detected... closing...")
			stop()
		}
	}()

	cli.Execute(ctx)
}