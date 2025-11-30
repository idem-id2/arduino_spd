@echo off
chcp 65001 >nul
HpeAdvancedAnalyzer.exe > analysis_result.txt 2>&1
type analysis_result.txt


