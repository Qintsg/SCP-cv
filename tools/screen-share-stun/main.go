// 文件描述：使用官方 Pion 为屏幕共享提供标准 STUN Binding 服务。
// 项目：SCP-cv；文件：main.go；作者：Qintsg；日期：2026-11-05。
package main

import (
	"context"
	"flag"
	"fmt"
	"log"
	"net"
	"os"
	"os/signal"
	"strconv"
	"syscall"

	"github.com/pion/turn/v5"
)

// main :returns: 读取 IPv4 监听参数并运行服务，终止信号触发正常关闭。
func main() {
	listen := flag.String("listen", "127.0.0.1:3478", "STUN 监听的 IPv4:端口")
	flag.Parse()
	if err := validateListen(*listen); err != nil {
		log.Fatal(err)
	}
	listener, err := net.ListenPacket("udp4", *listen)
	if err != nil {
		log.Fatal(err)
	}
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()
	if err := serve(ctx, listener); err != nil {
		log.Fatal(err)
	}
}

// validateListen :param address: IPv4 字面地址及端口。:returns: 明确的配置错误。
func validateListen(address string) error {
	host, port, err := net.SplitHostPort(address)
	if err != nil {
		return fmt.Errorf("监听地址需要 IPv4:端口：%w", err)
	}
	if ip := net.ParseIP(host); ip == nil || ip.To4() == nil {
		return fmt.Errorf("监听地址需要 IPv4 字面地址")
	}
	number, err := strconv.Atoi(port)
	if err != nil || number < 1 || number > 65535 {
		return fmt.Errorf("监听端口需要为 1–65535")
	}
	return nil
}

// serve :param ctx: 服务寿命。:param listener: 真实 UDP 监听。:returns: 停止后释放监听及 Pion 服务资源。
func serve(ctx context.Context, listener net.PacketConn) error {
	server, err := turn.NewServer(turn.ServerConfig{
		PacketConnConfigs: []turn.PacketConnConfig{{PacketConn: listener}},
	})
	if err != nil {
		_ = listener.Close()
		return err
	}
	log.Printf("STUN ready %s", listener.LocalAddr())
	<-ctx.Done()
	if err := server.Close(); err != nil {
		return err
	}
	log.Print("STUN stopped")
	return nil
}
