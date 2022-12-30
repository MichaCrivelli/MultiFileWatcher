# MultiFileWatcher

## Installation
Download the **setup.exe** or **installer.msi** from the latest release and run it. With that the service is installed and started.<br/>
After a sucessfull installation there should be a folder "MultiFileWatcher" on your System drive (C:\MultiFileWatcher in most cases). 

## What it does:
The MultiFileWatcher is a Windows service that lets you create mutliple SystemFileWatchers for directories on your system, which log any changes of files or directories in the specified folder.<br/>

## How to setup a FileWatcher.
In the "Repositories.conf" inside the "MultiFileWatcher" folder is where you manage your FileWatchers (Repositories as I call them).
Each line represents a Repository. Lines that start with # are ignored and function as comments.

### How a FileWatcher is added:
A Line must have at leas 4 arguments seperated by " | " (attention to the whitespaces)
1. **Repository Name** : MyRepo (Name for the LogFile)
2. **FileWatcher status** : WATCHING or PAUSED
3. **Exclusions** : myFile.txt:\MyFolder:.conf (Seperated by a ':')
4. **FolderPaht** : C:\FolderToWatch

#### Result : MyRepo | WATCHING | myFile.txt:\MyFolder:.conf | C:\FolderToWatch

## Exclusions
Multiple exclusions are seperated by a ':' -> "exclusion1:exclusion2"

### Exclusion by Path
The exclusion Argument must start with a '\\'! and represent a relative path of a file or folder<br/>
All changes of files or folders with the exclusion in the start of its relative path are ignored.
#### Example
**FolderPath**: C:\MyFolder<br/>
**ChangedFilePath**: C:\MyFolder\Important\details.txt<br/>
**RelativePath**: Important\details.txt<br/>
**Exclusion for single file**: \Important\details.txt<br/>
**Exclusion for all files in a folder**: \Important<br/>

### Exclusion by filetype
The Exclusion must start with a '.'<br/>
all files with this filetype are ignored.

### General Exclusion
Every exclusion not starting with a '\\' or a '.' exclude filenames.<br/>
For example: "test.txt" excludes all files with this exact filename (with extension)

## Logs
The folder "ChangeLogs" holds .csv files for every repository in the "Repositories.conf" file.
Each line represents a change and holds the following infomations:
1. Action: What happend to the file (CREATED, DELETED, CHANGED)
2. Datetime: When the change occured
3. Relative Path: path of the changed file or folder relative to the Repository folder<br/>

### A change might look like this
CREATED,30.12.2022 20:17:13,/Important/NewDetails.txt
