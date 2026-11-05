// 文件描述：用真实 UDP 与官方 STUN 客户端验证 Binding 及正常释放。
// 项目：SCP-cv；文件：main_test.go；作者：Qintsg；日期：2026-11-05。
package main

import (
	"context"
	"net"
	"testing"
	"time"

	"github.com/pion/stun/v4"
)

// TestRealBindingAndShutdown :param t: 测试上下文。:returns: 核对实际映射地址及关闭后端口可重新绑定。
func TestRealBindingAndShutdown(t *testing.T) {
	listener, err := net.ListenPacket("udp4", "127.0.0.1:0")
	if err != nil {
		t.Fatal(err)
	}
	address := listener.LocalAddr().String()
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()
	finished := make(chan error, 1)
	go func() { finished <- serve(ctx, listener) }()
	client, err := stun.Dial("udp4", address)
	if err != nil {
		t.Fatal(err)
	}
	defer client.Close()
	var mapped stun.XORMappedAddress
	var responseError error
	err = client.Do(stun.MustBuild(stun.TransactionID, stun.BindingRequest), func(event stun.Event) {
		if event.Error != nil {
			responseError = event.Error
			return
		}
		responseError = mapped.GetFrom(event.Message)
	})
	if err != nil {
		t.Fatal(err)
	}
	if responseError != nil {
		t.Fatal(responseError)
	}
	if !mapped.IP.Equal(net.ParseIP("127.0.0.1")) || mapped.Port == 0 {
		t.Fatalf("实际映射地址不符：%s", mapped.String())
	}
	cancel()
	select {
	case err := <-finished:
		if err != nil {
			t.Fatal(err)
		}
	case <-time.After(3 * time.Second):
		t.Fatal("服务没有正常关闭")
	}
	rebound, err := net.ListenPacket("udp4", address)
	if err != nil {
		t.Fatal(err)
	}
	_ = rebound.Close()
}
