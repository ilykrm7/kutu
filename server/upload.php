<?php
declare(strict_types=1);

/**
 * Kutu single-file upload endpoint.
 * Set KUTU_UPLOAD_TOKEN (or edit TOKEN) and optionally KUTU_PUBLIC_BASE.
 */

const MAX_BYTES = 15 * 1024 * 1024;
const TOKEN = 'change-me';

header('Content-Type: application/json; charset=utf-8');

function fail(int $code, string $message): void
{
    http_response_code($code);
    echo json_encode(['ok' => false, 'error' => $message], JSON_UNESCAPED_UNICODE);
    exit;
}

$expected = getenv('KUTU_UPLOAD_TOKEN');
if ($expected === false || $expected === '') {
    $expected = TOKEN;
}

$given = $_SERVER['HTTP_X_API_KEY'] ?? '';
if ($given === '' && isset($_SERVER['HTTP_AUTHORIZATION']) && str_starts_with($_SERVER['HTTP_AUTHORIZATION'], 'Bearer ')) {
    $given = substr($_SERVER['HTTP_AUTHORIZATION'], 7);
}

if ($expected === '' || $expected === 'change-me') {
    fail(500, 'Server token is not configured.');
}

if (!hash_equals($expected, $given)) {
    fail(401, 'Unauthorized');
}

$uploadDir = __DIR__ . '/uploads';
if (!is_dir($uploadDir) && !mkdir($uploadDir, 0755, true) && !is_dir($uploadDir)) {
    fail(500, 'Cannot create upload directory.');
}

$base = getenv('KUTU_PUBLIC_BASE');
if ($base === false || $base === '') {
    $https = !empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off';
    $scheme = $https ? 'https' : 'http';
    $host = $_SERVER['HTTP_HOST'] ?? 'localhost';
    $dir = rtrim(str_replace('\\', '/', dirname($_SERVER['SCRIPT_NAME'] ?? '')), '/');
    $base = $scheme . '://' . $host . $dir;
}

$allowed = ['png', 'jpg', 'jpeg', 'webp', 'gif', 'bmp', 'txt', 'md', 'json', 'pdf', 'ico', 'heic'];

if (!empty($_FILES['file']['tmp_name']) && is_uploaded_file($_FILES['file']['tmp_name'])) {
    $size = (int) ($_FILES['file']['size'] ?? 0);
    if ($size <= 0 || $size > MAX_BYTES) {
        fail(413, 'File too large.');
    }

    $original = (string) ($_FILES['file']['name'] ?? 'file.bin');
    $ext = strtolower(pathinfo($original, PATHINFO_EXTENSION));
    if (!in_array($ext, $allowed, true)) {
        fail(415, 'Extension not allowed.');
    }

    $name = bin2hex(random_bytes(16)) . '.' . $ext;
    $dest = $uploadDir . '/' . $name;
    if (!move_uploaded_file($_FILES['file']['tmp_name'], $dest)) {
        fail(500, 'Store failed.');
    }

    echo json_encode(['ok' => true, 'url' => $base . '/uploads/' . $name], JSON_UNESCAPED_SLASHES);
    exit;
}

$text = $_POST['text'] ?? '';
if (!is_string($text) || $text === '') {
    fail(400, 'No file or text.');
}

if (strlen($text) > MAX_BYTES) {
    fail(413, 'Text too large.');
}

$name = bin2hex(random_bytes(16)) . '.txt';
if (file_put_contents($uploadDir . '/' . $name, $text) === false) {
    fail(500, 'Store failed.');
}

echo json_encode(['ok' => true, 'url' => $base . '/uploads/' . $name], JSON_UNESCAPED_SLASHES);
