@echo off
setlocal

:: 设置要扫描的目录（当前目录）
set "target_dir=%cd%"

:: 设置输出文件
set "output_file=lib_list.txt"

:: 清空输出文件
> "%output_file%" (

    :: 遍历目录下所有的 .lib 文件（包括子目录）
    for /r "%target_dir%" %%f in (*.lib) do (
        echo %%~nxf
    )

)

echo 所有 .lib 文件名已保存到 %output_file%
pause
