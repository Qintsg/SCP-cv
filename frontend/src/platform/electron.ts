/** Electron平台薄适配：原生预览只暴露受限租约，不传媒体凭据和程序路径。 */
import type {
  ClientLifecycleState,
  PlatformAdapter,
  PlatformFile,
  SaveFileRequest,
} from './index';
import type { ServerProfile } from './connection';
import type { NativePreviewAdapter } from './previewTypes';

interface ElectronSelectedFile {
  name: string;
  mimeType: string;
  bytes: Uint8Array<ArrayBuffer>;
}

export interface ElectronBridge {
  readonly platform: 'electron';
  readonly preview?: NativePreviewAdapter;
  pickFile(accept?: readonly string[]): Promise<ElectronSelectedFile | null>;
  saveFile(request: { suggestedName: string; mimeType: string; bytes: Uint8Array }): Promise<boolean>;
  clearSession(): Promise<void>;
  loadServerProfile(): Promise<ServerProfile | null>;
  saveServerProfile(profile: ServerProfile): Promise<ServerProfile>;
  onLifecycleChanged(listener: (state: ClientLifecycleState) => void): () => void;
  onBackRequested(listener: () => void): () => void;
}

declare global {
  interface Window {
    scpCvElectron?: ElectronBridge;
  }
}

function requireBridge(): ElectronBridge {
  if (!window.scpCvElectron) throw new Error('Electron 原生桥不可用。');
  return window.scpCvElectron;
}

export function createElectronPlatformAdapter(bridge: ElectronBridge = requireBridge()): PlatformAdapter {
  return {
    platform: 'electron',
    isPackaged: true,
    preview: bridge.preview,
    onBackRequested(handler) {
      return bridge.onBackRequested(() => { void handler(); });
    },
    onLifecycleChanged(handler) {
      return bridge.onLifecycleChanged(handler);
    },
    async pickFile(accept = []): Promise<PlatformFile | null> {
      const selected = await bridge.pickFile(accept);
      if (!selected) return null;
      const bytes = Uint8Array.from(selected.bytes);
      const data = new Blob([bytes.buffer], { type: selected.mimeType || 'application/octet-stream' });
      return { name: selected.name, mimeType: data.type, size: data.size, data };
    },
    async saveFile(request: SaveFileRequest): Promise<boolean> {
      const bytes = new Uint8Array(await request.data.arrayBuffer());
      return bridge.saveFile({ suggestedName: request.suggestedName, mimeType: request.mimeType, bytes });
    },
    clearSession() {
      return bridge.clearSession();
    },
  };
}

export async function loadElectronServerProfile(bridge: ElectronBridge = requireBridge()): Promise<ServerProfile | null> {
  return bridge.loadServerProfile();
}

export async function saveElectronServerProfile(
  profile: ServerProfile,
  bridge: ElectronBridge = requireBridge(),
): Promise<ServerProfile> {
  return bridge.saveServerProfile(profile);
}
