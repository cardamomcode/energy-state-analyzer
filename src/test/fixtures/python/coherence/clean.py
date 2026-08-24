import os


def readConfig(path):
    return os.path.exists(path)


def writeConfig(path, data):
    return len(data) > 0
