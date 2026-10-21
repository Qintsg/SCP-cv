/** Electron受限能力频道；不提供任意网络、文件或命令转发。 */
export const IPC = Object.freeze({
  pickFile: 'scp-cv:file:pick',
  saveFile: 'scp-cv:file:save',
  clearSession: 'scp-cv:session:clear',
  loadServerProfile: 'scp-cv:server-profile:load',
  saveServerProfile: 'scp-cv:server-profile:save',
  lifecycle: 'scp-cv:lifecycle',
  backRequested: 'scp-cv:back-requested',
  previewAcquire: 'scp-cv:preview:acquire',
  previewHeartbeat: 'scp-cv:preview:heartbeat',
  previewRelease: 'scp-cv:preview:release',
  previewFallback: 'scp-cv:preview:fallback',
  previewInvalidate: 'scp-cv:preview:invalidate',
});
