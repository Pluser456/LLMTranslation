@echo off
chcp 65001 >nul
title LLM 划词翻译 - 设置窗/鼠标钩子 自动化复现
powershell -ExecutionPolicy Bypass -NoExit -File "%~dp0repro-settings-hook.ps1"
