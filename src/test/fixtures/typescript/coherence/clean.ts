import * as fs from 'fs';

function readConfig(path: string): boolean {
    return fs.existsSync(path);
}

function writeConfig(path: string, data: string): boolean {
    return data.length > 0;
}
