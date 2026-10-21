/** 只读启动诊断：区分Electron默认入口前退出与正式Main/app协议异常，不改安全策略。 */
const { writeFileSync } = require('node:fs');
const { dirname, join } = require('node:path');
const directory = process.env.SCP_CV_NATIVE_PREVIEW_TEST_REPORT ? dirname(process.env.SCP_CV_NATIVE_PREVIEW_TEST_REPORT) : '';
const diagnostic = { entered: true, electron: process.versions.electron, events: [] };
/** :returns: 只写本次指定证据目录，无认证／流原文。 */
function save() { if (directory) writeFileSync(join(directory, 'electron-entry.json'), JSON.stringify(diagnostic, null, 2) + '\n', 'utf8'); }
save();
process.on('uncaughtException', error => { diagnostic.events.push({ uncaught: error.message.replace(/rtsp:\/\/\S+/g, '[redacted]') }); save(); require('electron').app.exit(1); });
import('./runtime.mjs').catch(error => { diagnostic.events.push({ importFailure: error.message.replace(/rtsp:\/\/\S+/g, '[redacted]') }); save(); require('electron').app.exit(1); });
