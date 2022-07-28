// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

package common

import (
	"fmt"
	"io/ioutil"
	"os"
	"path/filepath"
)

func PathExists(path string) bool {
	_, err := os.Stat(path)
	return !os.IsNotExist(err)
}

func FileExists(path string) bool {
	fi, err := os.Stat(path)
	if err != nil {
		return false
	}
	return !fi.IsDir()
}

func GetSubPath(parentDir string, subDir string) string {
	parentDir = filepath.Clean(parentDir)
	subDir = filepath.Clean(subDir)
	if len(parentDir) >= len(subDir) {
		return ""
	}
	return subDir[len(parentDir)+1:]
}

// Gets the full path from a relative path, and ensure the path exists.
func GetValidatedFullPath(filePath string) string {
	fullAppPath, err := filepath.Abs(filePath)
	if err != nil {
		panic(err)
	}

	if _, err := os.Stat(fullAppPath); os.IsNotExist(err) {
		panic("Path '" + fullAppPath + "' does not exist.")
	}
	return fullAppPath
}

// Writes the entrypoint command to an executable file
func WriteScript(filePath string, command string) {
	fmt.Println("Writing output script to '" + filePath + "'")

	// Ensure directory
	dir := filepath.Dir(filePath)
	if !PathExists(dir) {
		os.MkdirAll(dir, os.ModePerm)
	}

	ioutil.WriteFile(filePath, []byte(command), 0755)
}

// Appends command to a file
func AppendScript(filePath string, command string) {
	if command == "" {
		return
	}
	fmt.Println("Appending output script to '" + filePath + "'")

	// Ensures directory
	dir := filepath.Dir(filePath)
	if !PathExists(dir) {
		return
	}

	file, err := os.OpenFile(filePath, os.O_APPEND|os.O_WRONLY, 0755)
	if err != nil {
		return
	}
	defer file.Close()

	// Appends the command at the end of the file
	if _, err := file.WriteString("\n" + command); err != nil {
		return
	}
}

// Try to add a permission to a file
func TryAddPermission(filePath string, permission os.FileMode) bool {
	err := os.Chmod(filePath, permission)
	if err != nil {
		return false
	}
	return true
}

// Check if the command is a file in app's repository and add execution permission to it
func ParseCommandAndAddExecutionPermission(commandString string, sourcePath string) bool {
	absoluteFilePath, err := filepath.Abs(filepath.Join(sourcePath, commandString))
	if err != nil {
		panic(err)
	} else {
		if FileExists(absoluteFilePath) {
			return TryAddPermission(absoluteFilePath, 0755)
		}
		if FileExists(commandString) {
			return TryAddPermission(commandString, 0755)
		}
		return false
	}
}

func ExtendPathForCommand(command string, sourcePath string) string {
	if command == "" {
		return command
	}
	return "PATH=\"$PATH:" + sourcePath + "\" " + command
}
