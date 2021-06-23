package main

import (
	"context"
	_ "embed"
	"fmt"
	log "github.com/sirupsen/logrus"
	"os"
	"os/signal"
	"toscactl/cli"
)

func main() {
	signalchan := make (chan os.Signal,1)
	signal.Notify(signalchan,os.Interrupt,os.Kill)
	ctx, stop :=context.WithCancel(context.Background())
	defer stop()
	go func() {
		select {
		case <-signalchan:
			fmt.Println()
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