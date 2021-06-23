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
	signalchan := make (chan os.Signal)
	signal.Notify(signalchan)
	ctx, stop :=context.WithCancel(context.Background())
	defer stop()
	go func() {
		select {
		case <-signalchan:
			log.Info("Program interruption detected... closing...")
			stop()
		}
	}()
/*
	go func(){
		time.Sleep(12 * time.Second)
		stop()
	}()
*/
	cli.Execute(ctx)
}