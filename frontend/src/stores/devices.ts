/*
 * 设备电源 Store：拼接屏 / 电视左 / 电视右 的 TCP 指令封装。
 * 设计稿 §4.1 + §4.6：仪表盘电源卡 + 设置「设备电源」Tab 共用。
 */
import { defineStore } from 'pinia';

import { api, type DeviceItem } from '@/services/api';
import { t } from '@/locales';
import { beginDeviceCommand, isDeviceCommandPending, resetDeviceCommands } from './deviceCommandScope';
import { isWritePending } from '@/services/writeFlights';

interface DeviceState {
  devices: DeviceItem[];
  /** 上次操作的执行时间戳（毫秒）；用于设置页展示「上次操作 13:42」。 */
  lastActionAt: Record<string, number>;
  /** 上次操作的语义结果：success/error。 */
  lastActionResult: Record<string, 'success' | 'error' | 'pending'>;
  /** 上次操作的错误描述。 */
  lastActionDetail: Record<string, string>;
}

export const useDeviceStore = defineStore('devices', {
  state: (): DeviceState => ({
    devices: [],
    lastActionAt: {},
    lastActionResult: {},
    lastActionDetail: {},
  }),
  getters: {
    spliceScreen: (state): DeviceItem | undefined =>
      state.devices.find((d) => d.device_type === 'splice_screen'),
    tvLeft: (state): DeviceItem | undefined => state.devices.find((d) => d.device_type === 'tv_left'),
    tvRight: (state): DeviceItem | undefined => state.devices.find((d) => d.device_type === 'tv_right'),
    /** :returns: 当前设备操作的实时忙态，不把旧主机的pending当作当前锁。 */
    isPending(state): (deviceType: string) => boolean {
      return deviceType => isWritePending(`device:${deviceType}`)
        || (state.lastActionResult[deviceType] === 'pending' && isDeviceCommandPending(this, deviceType));
    },
  },
  actions: {
    async refresh(): Promise<void> {
      const payload = await api.listDevices();
      this.devices = payload.devices;
    },
    /** 拼接屏：开机/关机；电视：toggle 切换不读真实状态。 */
    async power(deviceType: string, action: 'on' | 'off'): Promise<void> {
      const operation = beginDeviceCommand(this, deviceType);
      this.lastActionResult[deviceType] = 'pending';
      try {
        operation.assertCurrent();
        await api.powerDevice(deviceType, action);
        operation.assertCurrent();
        this.lastActionResult[deviceType] = 'success';
        this.lastActionDetail[deviceType] = action === 'on' ? t('settings.powerOnSent') : t('settings.powerOffSent');
      } catch (error) {
        if (operation.isCurrent()) {
          this.lastActionResult[deviceType] = 'error';
          this.lastActionDetail[deviceType] = error instanceof Error ? error.message : t('settings.deviceCmdFail');
        }
        throw error;
      } finally {
        if (operation.isCurrent()) this.lastActionAt[deviceType] = Date.now();
        operation.release();
      }
    },
    async toggle(deviceType: string): Promise<void> {
      const operation = beginDeviceCommand(this, deviceType);
      this.lastActionResult[deviceType] = 'pending';
      try {
        operation.assertCurrent();
        await api.toggleDevice(deviceType);
        operation.assertCurrent();
        this.lastActionResult[deviceType] = 'success';
        this.lastActionDetail[deviceType] = t('settings.cmdSent');
      } catch (error) {
        if (operation.isCurrent()) {
          this.lastActionResult[deviceType] = 'error';
          this.lastActionDetail[deviceType] = error instanceof Error ? error.message : t('settings.deviceToggleFail');
        }
        throw error;
      } finally {
        if (operation.isCurrent()) this.lastActionAt[deviceType] = Date.now();
        operation.release();
      }
    },
    /** :returns: 清空投影并使旧操作失效；旧finally不能释放reset后新建的锁。 */
    $reset(): void {
      resetDeviceCommands(this);
      this.$patch({ devices: [], lastActionAt: {}, lastActionResult: {}, lastActionDetail: {} });
    },
  },
});
