import { createCipheriv, createDecipheriv, pbkdf2Sync, randomBytes } from "node:crypto";
import { closeSync, createReadStream, createWriteStream, existsSync, openSync, readSync, rmSync, statSync, writeFileSync, appendFileSync } from "node:fs";
import { resolve } from "node:path";
import { pipeline } from "node:stream/promises";

const magic = Buffer.from("ETMBK001", "ascii");
const saltLength = 16;
const nonceLength = 12;
const authenticationTagLength = 16;
const keyLength = 32;
const derivationIterations = 200000;
const headerLength = magic.length + saltLength + nonceLength;

const [operation, inputPath, outputPath] = process.argv.slice(2);
const password = process.env.BACKUP_ENCRYPTION_PASSWORD;

if (!password)
{
    throw new Error("La variable BACKUP_ENCRYPTION_PASSWORD est absente.");
}

if (!operation || !inputPath || !outputPath)
{
    throw new Error("Utilisation : node backup-encryption.mjs <encrypt|decrypt> <entrée> <sortie>.");
}

if (resolve(inputPath) === resolve(outputPath))
{
    throw new Error("Les chemins d’entrée et de sortie doivent être différents.");
}

function deriveKey(salt)
{
    return pbkdf2Sync(Buffer.from(password, "utf8"), salt, derivationIterations, keyLength, "sha256");
}

function readBuffer(filePath, length, position)
{
    const descriptor = openSync(filePath, "r");

    try
    {
        const buffer = Buffer.alloc(length);
        const bytesRead = readSync(descriptor, buffer, 0, length, position);

        if (bytesRead !== length)
        {
            throw new Error(`Lecture incomplète de ${filePath}.`);
        }

        return buffer;
    }
    finally
    {
        closeSync(descriptor);
    }
}

async function encrypt()
{
    const salt = randomBytes(saltLength);
    const nonce = randomBytes(nonceLength);
    const key = deriveKey(salt);
    const cipher = createCipheriv("aes-256-gcm", key, nonce);

    try
    {
        writeFileSync(outputPath, Buffer.concat([magic, salt, nonce]), { mode: 0o600 });

        await pipeline(
            createReadStream(inputPath),
            cipher,
            createWriteStream(outputPath, { flags: "a", mode: 0o600 }));

        appendFileSync(outputPath, cipher.getAuthTag());
    }
    catch (error)
    {
        if (existsSync(outputPath))
        {
            rmSync(outputPath);
        }

        throw error;
    }
    finally
    {
        key.fill(0);
    }
}

async function decrypt()
{
    const encryptedSize = statSync(inputPath).size;
    const minimumSize = headerLength + authenticationTagLength;

    if (encryptedSize < minimumSize)
    {
        throw new Error("La sauvegarde chiffrée est incomplète.");
    }

    const header = readBuffer(inputPath, headerLength, 0);
    const storedMagic = header.subarray(0, magic.length);

    if (!storedMagic.equals(magic))
    {
        throw new Error("Le format de la sauvegarde chiffrée est inconnu.");
    }

    const salt = header.subarray(magic.length, magic.length + saltLength);
    const nonce = header.subarray(magic.length + saltLength, headerLength);
    const authenticationTag = readBuffer(inputPath, authenticationTagLength, encryptedSize - authenticationTagLength);
    const key = deriveKey(salt);
    const decipher = createDecipheriv("aes-256-gcm", key, nonce);

    decipher.setAuthTag(authenticationTag);

    try
    {
        await pipeline(
            createReadStream(inputPath, { start: headerLength, end: encryptedSize - authenticationTagLength - 1 }),
            decipher,
            createWriteStream(outputPath, { mode: 0o600 }));
    }
    catch (error)
    {
        if (existsSync(outputPath))
        {
            rmSync(outputPath);
        }

        throw error;
    }
    finally
    {
        key.fill(0);
    }
}

if (operation === "encrypt")
{
    await encrypt();
}
else if (operation === "decrypt")
{
    await decrypt();
}
else
{
    throw new Error(`Opération inconnue : ${operation}.`);
}