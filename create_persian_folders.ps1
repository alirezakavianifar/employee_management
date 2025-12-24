# Set output encoding to UTF-8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$workersPath = "D:\projects\employee_management_csharp\SharedData\Workers"
$sourceImg = "C:\Users\Administrator\Downloads\employee.png"

# Check if source image exists
if (-not (Test-Path $sourceImg)) {
    Write-Error "Source image not found: $sourceImg"
    exit 1
}

# Ensure Workers directory exists
if (-not (Test-Path $workersPath)) {
    New-Item -ItemType Directory -Path $workersPath -Force | Out-Null
}

# Create مریم_علیزاده folder and files using Unicode code points
# مریم = U+0645 U+0631 U+06CC U+0645
# علیزاده = U+0639 U+0644 U+06CC U+0632 U+0627 U+062F U+0647
$folder1Name = [char]0x0645 + [char]0x0631 + [char]0x06CC + [char]0x0645 + "_" + [char]0x0639 + [char]0x0644 + [char]0x06CC + [char]0x0632 + [char]0x0627 + [char]0x062F + [char]0x0647
$folder1 = Join-Path $workersPath $folder1Name
if (-not (Test-Path $folder1)) {
    New-Item -ItemType Directory -Path $folder1 -Force | Out-Null
}
$file1_401 = $folder1Name + "_401.jpg"
$file1_402 = $folder1Name + "_402.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder1 $file1_401) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder1 $file1_402) -Force

# Create علی_احمدی folder and file using Unicode code points
# علی = U+0639 U+0644 U+06CC
# احمدی = U+0627 U+062D U+0645 U+062F U+06CC
$folder2Name = [char]0x0639 + [char]0x0644 + [char]0x06CC + "_" + [char]0x0627 + [char]0x062D + [char]0x0645 + [char]0x062F + [char]0x06CC
$folder2 = Join-Path $workersPath $folder2Name
if (-not (Test-Path $folder2)) {
    New-Item -ItemType Directory -Path $folder2 -Force | Out-Null
}
$file2_501 = $folder2Name + "_501.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder2 $file2_501) -Force

# Create فاطمه_محمدی folder and files
# فاطمه = U+0641 U+0627 U+0637 U+0645 U+0647
# محمدی = U+0645 U+062D U+0645 U+062F U+06CC
$folder3Name = [char]0x0641 + [char]0x0627 + [char]0x0637 + [char]0x0645 + [char]0x0647 + "_" + [char]0x0645 + [char]0x062D + [char]0x0645 + [char]0x062F + [char]0x06CC
$folder3 = Join-Path $workersPath $folder3Name
if (-not (Test-Path $folder3)) {
    New-Item -ItemType Directory -Path $folder3 -Force | Out-Null
}
$file3_601 = $folder3Name + "_601.jpg"
$file3_602 = $folder3Name + "_602.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder3 $file3_601) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder3 $file3_602) -Force

# Create حسن_رضایی folder and files
# حسن = U+062D U+0633 U+0646
# رضایی = U+0631 U+0636 U+0627 U+06CC U+06CC
$folder4Name = [char]0x062D + [char]0x0633 + [char]0x0646 + "_" + [char]0x0631 + [char]0x0636 + [char]0x0627 + [char]0x06CC + [char]0x06CC
$folder4 = Join-Path $workersPath $folder4Name
if (-not (Test-Path $folder4)) {
    New-Item -ItemType Directory -Path $folder4 -Force | Out-Null
}
$file4_701 = $folder4Name + "_701.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder4 $file4_701) -Force

# Create زهرا_کریمی folder and files
# زهرا = U+0632 U+0647 U+0631 U+0627
# کریمی = U+06A9 U+0631 U+06CC U+0645 U+06CC
$folder5Name = [char]0x0632 + [char]0x0647 + [char]0x0631 + [char]0x0627 + "_" + [char]0x06A9 + [char]0x0631 + [char]0x06CC + [char]0x0645 + [char]0x06CC
$folder5 = Join-Path $workersPath $folder5Name
if (-not (Test-Path $folder5)) {
    New-Item -ItemType Directory -Path $folder5 -Force | Out-Null
}
$file5_801 = $folder5Name + "_801.jpg"
$file5_802 = $folder5Name + "_802.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder5 $file5_801) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder5 $file5_802) -Force

# Create محمود_حسینی folder and files
# محمود = U+0645 U+062D U+0645 U+0648 U+062F
# حسینی = U+062D U+0633 U+06CC U+0646 U+06CC
$folder6Name = [char]0x0645 + [char]0x062D + [char]0x0645 + [char]0x0648 + [char]0x062F + "_" + [char]0x062D + [char]0x0633 + [char]0x06CC + [char]0x0646 + [char]0x06CC
$folder6 = Join-Path $workersPath $folder6Name
if (-not (Test-Path $folder6)) {
    New-Item -ItemType Directory -Path $folder6 -Force | Out-Null
}
$file6_901 = $folder6Name + "_901.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder6 $file6_901) -Force

# Create سارا_نوری folder and files
# سارا = U+0633 U+0627 U+0631 U+0627
# نوری = U+0646 U+0648 U+0631 U+06CC
$folder7Name = [char]0x0633 + [char]0x0627 + [char]0x0631 + [char]0x0627 + "_" + [char]0x0646 + [char]0x0648 + [char]0x0631 + [char]0x06CC
$folder7 = Join-Path $workersPath $folder7Name
if (-not (Test-Path $folder7)) {
    New-Item -ItemType Directory -Path $folder7 -Force | Out-Null
}
$file7_1001 = $folder7Name + "_1001.jpg"
$file7_1002 = $folder7Name + "_1002.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder7 $file7_1001) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder7 $file7_1002) -Force

# Create رضا_موسوی folder and files
# رضا = U+0631 U+0636 U+0627
# موسوی = U+0645 U+0648 U+0633 U+0648 U+06CC
$folder8Name = [char]0x0631 + [char]0x0636 + [char]0x0627 + "_" + [char]0x0645 + [char]0x0648 + [char]0x0633 + [char]0x0648 + [char]0x06CC
$folder8 = Join-Path $workersPath $folder8Name
if (-not (Test-Path $folder8)) {
    New-Item -ItemType Directory -Path $folder8 -Force | Out-Null
}
$file8_1101 = $folder8Name + "_1101.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder8 $file8_1101) -Force

# Create نرگس_صادقی folder and files
# نرگس = U+0646 U+0631 U+06AF U+0633
# صادقی = U+0635 U+0627 U+062F U+0642 U+06CC
$folder9Name = [char]0x0646 + [char]0x0631 + [char]0x06AF + [char]0x0633 + "_" + [char]0x0635 + [char]0x0627 + [char]0x062F + [char]0x0642 + [char]0x06CC
$folder9 = Join-Path $workersPath $folder9Name
if (-not (Test-Path $folder9)) {
    New-Item -ItemType Directory -Path $folder9 -Force | Out-Null
}
$file9_1201 = $folder9Name + "_1201.jpg"
$file9_1202 = $folder9Name + "_1202.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder9 $file9_1201) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder9 $file9_1202) -Force

# Create امیر_جعفری folder and files
# امیر = U+0627 U+0645 U+06CC U+0631
# جعفری = U+062C U+0639 U+0641 U+0631 U+06CC
$folder10Name = [char]0x0627 + [char]0x0645 + [char]0x06CC + [char]0x0631 + "_" + [char]0x062C + [char]0x0639 + [char]0x0641 + [char]0x0631 + [char]0x06CC
$folder10 = Join-Path $workersPath $folder10Name
if (-not (Test-Path $folder10)) {
    New-Item -ItemType Directory -Path $folder10 -Force | Out-Null
}
$file10_1301 = $folder10Name + "_1301.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder10 $file10_1301) -Force

# Create لیلا_باقری folder and files
# لیلا = U+0644 U+06CC U+0644 U+0627
# باقری = U+0628 U+0627 U+0642 U+0631 U+06CC
$folder11Name = [char]0x0644 + [char]0x06CC + [char]0x0644 + [char]0x0627 + "_" + [char]0x0628 + [char]0x0627 + [char]0x0642 + [char]0x0631 + [char]0x06CC
$folder11 = Join-Path $workersPath $folder11Name
if (-not (Test-Path $folder11)) {
    New-Item -ItemType Directory -Path $folder11 -Force | Out-Null
}
$file11_1401 = $folder11Name + "_1401.jpg"
$file11_1402 = $folder11Name + "_1402.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder11 $file11_1401) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder11 $file11_1402) -Force

# Create حسین_اکبری folder and files
# حسین = U+062D U+0633 U+06CC U+0646
# اکبری = U+0627 U+06A9 U+0628 U+0631 U+06CC
$folder12Name = [char]0x062D + [char]0x0633 + [char]0x06CC + [char]0x0646 + "_" + [char]0x0627 + [char]0x06A9 + [char]0x0628 + [char]0x0631 + [char]0x06CC
$folder12 = Join-Path $workersPath $folder12Name
if (-not (Test-Path $folder12)) {
    New-Item -ItemType Directory -Path $folder12 -Force | Out-Null
}
$file12_1501 = $folder12Name + "_1501.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder12 $file12_1501) -Force

# Create احمد_شریفی folder and files
# احمد = U+0627 U+062D U+0645 U+062F
# شریفی = U+0634 U+0631 U+06CC U+0641 U+06CC
$folder13Name = [char]0x0627 + [char]0x062D + [char]0x0645 + [char]0x062F + "_" + [char]0x0634 + [char]0x0631 + [char]0x06CC + [char]0x0641 + [char]0x06CC
$folder13 = Join-Path $workersPath $folder13Name
if (-not (Test-Path $folder13)) {
    New-Item -ItemType Directory -Path $folder13 -Force | Out-Null
}
$file13_1601 = $folder13Name + "_1601.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder13 $file13_1601) -Force

# Create سمیرا_یزدی folder and files
# سمیرا = U+0633 U+0645 U+06CC U+0631 U+0627
# یزدی = U+06CC U+0632 U+062F U+06CC
$folder14Name = [char]0x0633 + [char]0x0645 + [char]0x06CC + [char]0x0631 + [char]0x0627 + "_" + [char]0x06CC + [char]0x0632 + [char]0x062F + [char]0x06CC
$folder14 = Join-Path $workersPath $folder14Name
if (-not (Test-Path $folder14)) {
    New-Item -ItemType Directory -Path $folder14 -Force | Out-Null
}
$file14_1701 = $folder14Name + "_1701.jpg"
$file14_1702 = $folder14Name + "_1702.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder14 $file14_1701) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder14 $file14_1702) -Force

# Create داریوش_طاهری folder and files
# داریوش = U+062F U+0627 U+0631 U+06CC U+0648 U+0634
# طاهری = U+0637 U+0627 U+0647 U+0631 U+06CC
$folder15Name = [char]0x062F + [char]0x0627 + [char]0x0631 + [char]0x06CC + [char]0x0648 + [char]0x0634 + "_" + [char]0x0637 + [char]0x0627 + [char]0x0647 + [char]0x0631 + [char]0x06CC
$folder15 = Join-Path $workersPath $folder15Name
if (-not (Test-Path $folder15)) {
    New-Item -ItemType Directory -Path $folder15 -Force | Out-Null
}
$file15_1801 = $folder15Name + "_1801.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder15 $file15_1801) -Force

# Create نگار_فرهادی folder and files
# نگار = U+0646 U+06AF U+0627 U+0631
# فرهادی = U+0641 U+0631 U+0647 U+0627 U+062F U+06CC
$folder16Name = [char]0x0646 + [char]0x06AF + [char]0x0627 + [char]0x0631 + "_" + [char]0x0641 + [char]0x0631 + [char]0x0647 + [char]0x0627 + [char]0x062F + [char]0x06CC
$folder16 = Join-Path $workersPath $folder16Name
if (-not (Test-Path $folder16)) {
    New-Item -ItemType Directory -Path $folder16 -Force | Out-Null
}
$file16_1901 = $folder16Name + "_1901.jpg"
$file16_1902 = $folder16Name + "_1902.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder16 $file16_1901) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder16 $file16_1902) -Force

# Create کامران_نظری folder and files
# کامران = U+06A9 U+0627 U+0645 U+0631 U+0627 U+0646
# نظری = U+0646 U+0638 U+0631 U+06CC
$folder17Name = [char]0x06A9 + [char]0x0627 + [char]0x0645 + [char]0x0631 + [char]0x0627 + [char]0x0646 + "_" + [char]0x0646 + [char]0x0638 + [char]0x0631 + [char]0x06CC
$folder17 = Join-Path $workersPath $folder17Name
if (-not (Test-Path $folder17)) {
    New-Item -ItemType Directory -Path $folder17 -Force | Out-Null
}
$file17_2001 = $folder17Name + "_2001.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder17 $file17_2001) -Force

# Create شیدا_مهدوی folder and files
# شیدا = U+0634 U+06CC U+062F U+0627
# مهدوی = U+0645 U+0647 U+062F U+0648 U+06CC
$folder18Name = [char]0x0634 + [char]0x06CC + [char]0x062F + [char]0x0627 + "_" + [char]0x0645 + [char]0x0647 + [char]0x062F + [char]0x0648 + [char]0x06CC
$folder18 = Join-Path $workersPath $folder18Name
if (-not (Test-Path $folder18)) {
    New-Item -ItemType Directory -Path $folder18 -Force | Out-Null
}
$file18_2101 = $folder18Name + "_2101.jpg"
$file18_2102 = $folder18Name + "_2102.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder18 $file18_2101) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder18 $file18_2102) -Force

# Create بهرام_کاظمی folder and files
# بهرام = U+0628 U+0647 U+0631 U+0627 U+0645
# کاظمی = U+06A9 U+0627 U+0638 U+0645 U+06CC
$folder19Name = [char]0x0628 + [char]0x0647 + [char]0x0631 + [char]0x0627 + [char]0x0645 + "_" + [char]0x06A9 + [char]0x0627 + [char]0x0638 + [char]0x0645 + [char]0x06CC
$folder19 = Join-Path $workersPath $folder19Name
if (-not (Test-Path $folder19)) {
    New-Item -ItemType Directory -Path $folder19 -Force | Out-Null
}
$file19_2201 = $folder19Name + "_2201.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder19 $file19_2201) -Force

# Create پریسا_قاسمی folder and files
# پریسا = U+067E U+0631 U+06CC U+0633 U+0627
# قاسمی = U+0642 U+0627 U+0633 U+0645 U+06CC
$folder20Name = [char]0x067E + [char]0x0631 + [char]0x06CC + [char]0x0633 + [char]0x0627 + "_" + [char]0x0642 + [char]0x0627 + [char]0x0633 + [char]0x0645 + [char]0x06CC
$folder20 = Join-Path $workersPath $folder20Name
if (-not (Test-Path $folder20)) {
    New-Item -ItemType Directory -Path $folder20 -Force | Out-Null
}
$file20_2301 = $folder20Name + "_2301.jpg"
$file20_2302 = $folder20Name + "_2302.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder20 $file20_2301) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder20 $file20_2302) -Force

# Create فرهاد_عزیزی folder and files
# فرهاد = U+0641 U+0631 U+0647 U+0627 U+062F
# عزیزی = U+0639 U+0632 U+06CC U+0632 U+06CC
$folder21Name = [char]0x0641 + [char]0x0631 + [char]0x0647 + [char]0x0627 + [char]0x062F + "_" + [char]0x0639 + [char]0x0632 + [char]0x06CC + [char]0x0632 + [char]0x06CC
$folder21 = Join-Path $workersPath $folder21Name
if (-not (Test-Path $folder21)) {
    New-Item -ItemType Directory -Path $folder21 -Force | Out-Null
}
$file21_2401 = $folder21Name + "_2401.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder21 $file21_2401) -Force

# Create مهسا_رحیمی folder and files
# مهسا = U+0645 U+0647 U+0633 U+0627
# رحیمی = U+0631 U+062D U+06CC U+0645 U+06CC
$folder22Name = [char]0x0645 + [char]0x0647 + [char]0x0633 + [char]0x0627 + "_" + [char]0x0631 + [char]0x062D + [char]0x06CC + [char]0x0645 + [char]0x06CC
$folder22 = Join-Path $workersPath $folder22Name
if (-not (Test-Path $folder22)) {
    New-Item -ItemType Directory -Path $folder22 -Force | Out-Null
}
$file22_2501 = $folder22Name + "_2501.jpg"
$file22_2502 = $folder22Name + "_2502.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder22 $file22_2501) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder22 $file22_2502) -Force

# Create سعید_غلامی folder and files
# سعید = U+0633 U+0639 U+06CC U+062F
# غلامی = U+063A U+0644 U+0627 U+0645 U+06CC
$folder23Name = [char]0x0633 + [char]0x0639 + [char]0x06CC + [char]0x062F + "_" + [char]0x063A + [char]0x0644 + [char]0x0627 + [char]0x0645 + [char]0x06CC
$folder23 = Join-Path $workersPath $folder23Name
if (-not (Test-Path $folder23)) {
    New-Item -ItemType Directory -Path $folder23 -Force | Out-Null
}
$file23_2601 = $folder23Name + "_2601.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder23 $file23_2601) -Force

# Create مریم_رستمی folder and files
# مریم = U+0645 U+0631 U+06CC U+0645
# رستمی = U+0631 U+0633 U+062A U+0645 U+06CC
$folder24Name = [char]0x0645 + [char]0x0631 + [char]0x06CC + [char]0x0645 + "_" + [char]0x0631 + [char]0x0633 + [char]0x062A + [char]0x0645 + [char]0x06CC
$folder24 = Join-Path $workersPath $folder24Name
if (-not (Test-Path $folder24)) {
    New-Item -ItemType Directory -Path $folder24 -Force | Out-Null
}
$file24_2701 = $folder24Name + "_2701.jpg"
$file24_2702 = $folder24Name + "_2702.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder24 $file24_2701) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder24 $file24_2702) -Force

# Create علی_میرزایی folder and files
# علی = U+0639 U+0644 U+06CC
# میرزایی = U+0645 U+06CC U+0631 U+0632 U+0627 U+06CC U+06CC
$folder25Name = [char]0x0639 + [char]0x0644 + [char]0x06CC + "_" + [char]0x0645 + [char]0x06CC + [char]0x0631 + [char]0x0632 + [char]0x0627 + [char]0x06CC + [char]0x06CC
$folder25 = Join-Path $workersPath $folder25Name
if (-not (Test-Path $folder25)) {
    New-Item -ItemType Directory -Path $folder25 -Force | Out-Null
}
$file25_2801 = $folder25Name + "_2801.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder25 $file25_2801) -Force

# Create فریبا_شاهینی folder and files
# فریبا = U+0641 U+0631 U+06CC U+0628 U+0627
# شاهینی = U+0634 U+0627 U+0647 U+06CC U+0646 U+06CC
$folder26Name = [char]0x0641 + [char]0x0631 + [char]0x06CC + [char]0x0628 + [char]0x0627 + "_" + [char]0x0634 + [char]0x0627 + [char]0x0647 + [char]0x06CC + [char]0x0646 + [char]0x06CC
$folder26 = Join-Path $workersPath $folder26Name
if (-not (Test-Path $folder26)) {
    New-Item -ItemType Directory -Path $folder26 -Force | Out-Null
}
$file26_2901 = $folder26Name + "_2901.jpg"
$file26_2902 = $folder26Name + "_2902.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder26 $file26_2901) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder26 $file26_2902) -Force

# Create مهدی_صالحی folder and files
# مهدی = U+0645 U+0647 U+062F U+06CC
# صالحی = U+0635 U+0627 U+0644 U+062D U+06CC
$folder27Name = [char]0x0645 + [char]0x0647 + [char]0x062F + [char]0x06CC + "_" + [char]0x0635 + [char]0x0627 + [char]0x0644 + [char]0x062D + [char]0x06CC
$folder27 = Join-Path $workersPath $folder27Name
if (-not (Test-Path $folder27)) {
    New-Item -ItemType Directory -Path $folder27 -Force | Out-Null
}
$file27_3001 = $folder27Name + "_3001.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder27 $file27_3001) -Force

# Create نازنین_امینی folder and files
# نازنین = U+0646 U+0627 U+0632 U+0646 U+06CC U+0646
# امینی = U+0627 U+0645 U+06CC U+0646 U+06CC
$folder28Name = [char]0x0646 + [char]0x0627 + [char]0x0632 + [char]0x0646 + [char]0x06CC + [char]0x0646 + "_" + [char]0x0627 + [char]0x0645 + [char]0x06CC + [char]0x0646 + [char]0x06CC
$folder28 = Join-Path $workersPath $folder28Name
if (-not (Test-Path $folder28)) {
    New-Item -ItemType Directory -Path $folder28 -Force | Out-Null
}
$file28_3101 = $folder28Name + "_3101.jpg"
$file28_3102 = $folder28Name + "_3102.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder28 $file28_3101) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder28 $file28_3102) -Force

# Create کاوه_حیدری folder and files
# کاوه = U+06A9 U+0627 U+0648 U+0647
# حیدری = U+062D U+06CC U+062F U+0631 U+06CC
$folder29Name = [char]0x06A9 + [char]0x0627 + [char]0x0648 + [char]0x0647 + "_" + [char]0x062D + [char]0x06CC + [char]0x062F + [char]0x0631 + [char]0x06CC
$folder29 = Join-Path $workersPath $folder29Name
if (-not (Test-Path $folder29)) {
    New-Item -ItemType Directory -Path $folder29 -Force | Out-Null
}
$file29_3201 = $folder29Name + "_3201.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder29 $file29_3201) -Force

# Create یاسمن_موسوی folder and files
# یاسمن = U+06CC U+0627 U+0633 U+0645 U+0646
# موسوی = U+0645 U+0648 U+0633 U+0648 U+06CC
$folder30Name = [char]0x06CC + [char]0x0627 + [char]0x0633 + [char]0x0645 + [char]0x0646 + "_" + [char]0x0645 + [char]0x0648 + [char]0x0633 + [char]0x0648 + [char]0x06CC
$folder30 = Join-Path $workersPath $folder30Name
if (-not (Test-Path $folder30)) {
    New-Item -ItemType Directory -Path $folder30 -Force | Out-Null
}
$file30_3301 = $folder30Name + "_3301.jpg"
$file30_3302 = $folder30Name + "_3302.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder30 $file30_3301) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder30 $file30_3302) -Force

# Create بهروز_نوری folder and files
# بهروز = U+0628 U+0647 U+0631 U+0648 U+0632
# نوری = U+0646 U+0648 U+0631 U+06CC
$folder31Name = [char]0x0628 + [char]0x0647 + [char]0x0631 + [char]0x0648 + [char]0x0632 + "_" + [char]0x0646 + [char]0x0648 + [char]0x0631 + [char]0x06CC
$folder31 = Join-Path $workersPath $folder31Name
if (-not (Test-Path $folder31)) {
    New-Item -ItemType Directory -Path $folder31 -Force | Out-Null
}
$file31_3401 = $folder31Name + "_3401.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder31 $file31_3401) -Force

# Create گیتی_خانی folder and files
# گیتی = U+06AF U+06CC U+062A U+06CC
# خانی = U+062E U+0627 U+0646 U+06CC
$folder32Name = [char]0x06AF + [char]0x06CC + [char]0x062A + [char]0x06CC + "_" + [char]0x062E + [char]0x0627 + [char]0x0646 + [char]0x06CC
$folder32 = Join-Path $workersPath $folder32Name
if (-not (Test-Path $folder32)) {
    New-Item -ItemType Directory -Path $folder32 -Force | Out-Null
}
$file32_3501 = $folder32Name + "_3501.jpg"
$file32_3502 = $folder32Name + "_3502.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder32 $file32_3501) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder32 $file32_3502) -Force

# Create ایرج_عباسی folder and files
# ایرج = U+0627 U+06CC U+0631 U+062C
# عباسی = U+0639 U+0628 U+0627 U+0633 U+06CC
$folder33Name = [char]0x0627 + [char]0x06CC + [char]0x0631 + [char]0x062C + "_" + [char]0x0639 + [char]0x0628 + [char]0x0627 + [char]0x0633 + [char]0x06CC
$folder33 = Join-Path $workersPath $folder33Name
if (-not (Test-Path $folder33)) {
    New-Item -ItemType Directory -Path $folder33 -Force | Out-Null
}
$file33_3601 = $folder33Name + "_3601.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder33 $file33_3601) -Force

# Create الهه_داوودی folder and files
# الهه = U+0627 U+0644 U+0647 U+0647
# داوودی = U+062F U+0627 U+0648 U+0648 U+062F U+06CC
$folder34Name = [char]0x0627 + [char]0x0644 + [char]0x0647 + [char]0x0647 + "_" + [char]0x062F + [char]0x0627 + [char]0x0648 + [char]0x0648 + [char]0x062F + [char]0x06CC
$folder34 = Join-Path $workersPath $folder34Name
if (-not (Test-Path $folder34)) {
    New-Item -ItemType Directory -Path $folder34 -Force | Out-Null
}
$file34_3701 = $folder34Name + "_3701.jpg"
$file34_3702 = $folder34Name + "_3702.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder34 $file34_3701) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder34 $file34_3702) -Force

# Create فرید_ملکی folder and files
# فرید = U+0641 U+0631 U+06CC U+062F
# ملکی = U+0645 U+0644 U+06A9 U+06CC
$folder35Name = [char]0x0641 + [char]0x0631 + [char]0x06CC + [char]0x062F + "_" + [char]0x0645 + [char]0x0644 + [char]0x06A9 + [char]0x06CC
$folder35 = Join-Path $workersPath $folder35Name
if (-not (Test-Path $folder35)) {
    New-Item -ItemType Directory -Path $folder35 -Force | Out-Null
}
$file35_3801 = $folder35Name + "_3801.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder35 $file35_3801) -Force

# Create راضیه_زارعی folder and files
# راضیه = U+0631 U+0627 U+0636 U+06CC U+0647
# زارعی = U+0632 U+0627 U+0631 U+0639 U+06CC
$folder36Name = [char]0x0631 + [char]0x0627 + [char]0x0636 + [char]0x06CC + [char]0x0647 + "_" + [char]0x0632 + [char]0x0627 + [char]0x0631 + [char]0x0639 + [char]0x06CC
$folder36 = Join-Path $workersPath $folder36Name
if (-not (Test-Path $folder36)) {
    New-Item -ItemType Directory -Path $folder36 -Force | Out-Null
}
$file36_3901 = $folder36Name + "_3901.jpg"
$file36_3902 = $folder36Name + "_3902.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder36 $file36_3901) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder36 $file36_3902) -Force

# Create وحید_سلیمی folder and files
# وحید = U+0648 U+062D U+06CC U+062F
# سلیمی = U+0633 U+0644 U+06CC U+0645 U+06CC
$folder37Name = [char]0x0648 + [char]0x062D + [char]0x06CC + [char]0x062F + "_" + [char]0x0633 + [char]0x0644 + [char]0x06CC + [char]0x0645 + [char]0x06CC
$folder37 = Join-Path $workersPath $folder37Name
if (-not (Test-Path $folder37)) {
    New-Item -ItemType Directory -Path $folder37 -Force | Out-Null
}
$file37_4001 = $folder37Name + "_4001.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder37 $file37_4001) -Force

# Create شهرزاد_بهرامی folder and files
# شهرزاد = U+0634 U+0647 U+0631 U+0632 U+0627 U+062F
# بهرامی = U+0628 U+0647 U+0631 U+0627 U+0645 U+06CC
$folder38Name = [char]0x0634 + [char]0x0647 + [char]0x0631 + [char]0x0632 + [char]0x0627 + [char]0x062F + "_" + [char]0x0628 + [char]0x0647 + [char]0x0631 + [char]0x0627 + [char]0x0645 + [char]0x06CC
$folder38 = Join-Path $workersPath $folder38Name
if (-not (Test-Path $folder38)) {
    New-Item -ItemType Directory -Path $folder38 -Force | Out-Null
}
$file38_4101 = $folder38Name + "_4101.jpg"
$file38_4102 = $folder38Name + "_4102.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder38 $file38_4101) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder38 $file38_4102) -Force

# Create پیمان_رنجبر folder and files
# پیمان = U+067E U+06CC U+0645 U+0627 U+0646
# رنجبر = U+0631 U+0646 U+062C U+0628 U+0631
$folder39Name = [char]0x067E + [char]0x06CC + [char]0x0645 + [char]0x0627 + [char]0x0646 + "_" + [char]0x0631 + [char]0x0646 + [char]0x062C + [char]0x0628 + [char]0x0631
$folder39 = Join-Path $workersPath $folder39Name
if (-not (Test-Path $folder39)) {
    New-Item -ItemType Directory -Path $folder39 -Force | Out-Null
}
$file39_4201 = $folder39Name + "_4201.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder39 $file39_4201) -Force

# Create طاهره_شاهمرادی folder and files
# طاهره = U+0637 U+0627 U+0647 U+0631 U+0647
# شاهمرادی = U+0634 U+0627 U+0647 U+0645 U+0631 U+0627 U+062F U+06CC
$folder40Name = [char]0x0637 + [char]0x0627 + [char]0x0647 + [char]0x0631 + [char]0x0647 + "_" + [char]0x0634 + [char]0x0627 + [char]0x0647 + [char]0x0645 + [char]0x0631 + [char]0x0627 + [char]0x062F + [char]0x06CC
$folder40 = Join-Path $workersPath $folder40Name
if (-not (Test-Path $folder40)) {
    New-Item -ItemType Directory -Path $folder40 -Force | Out-Null
}
$file40_4301 = $folder40Name + "_4301.jpg"
$file40_4302 = $folder40Name + "_4302.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder40 $file40_4301) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder40 $file40_4302) -Force

# Create جواد_مختاری folder and files
# جواد = U+062C U+0648 U+0627 U+062F
# مختاری = U+0645 U+062E U+062A U+0627 U+0631 U+06CC
$folder41Name = [char]0x062C + [char]0x0648 + [char]0x0627 + [char]0x062F + "_" + [char]0x0645 + [char]0x062E + [char]0x062A + [char]0x0627 + [char]0x0631 + [char]0x06CC
$folder41 = Join-Path $workersPath $folder41Name
if (-not (Test-Path $folder41)) {
    New-Item -ItemType Directory -Path $folder41 -Force | Out-Null
}
$file41_4401 = $folder41Name + "_4401.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder41 $file41_4401) -Force

# Create نیلوفر_حسنی folder and files
# نیلوفر = U+0646 U+06CC U+0644 U+0648 U+0641 U+0631
# حسنی = U+062D U+0633 U+0646 U+06CC
$folder42Name = [char]0x0646 + [char]0x06CC + [char]0x0644 + [char]0x0648 + [char]0x0641 + [char]0x0631 + "_" + [char]0x062D + [char]0x0633 + [char]0x0646 + [char]0x06CC
$folder42 = Join-Path $workersPath $folder42Name
if (-not (Test-Path $folder42)) {
    New-Item -ItemType Directory -Path $folder42 -Force | Out-Null
}
$file42_4501 = $folder42Name + "_4501.jpg"
$file42_4502 = $folder42Name + "_4502.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder42 $file42_4501) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder42 $file42_4502) -Force

# Create کیوان_نصیری folder and files
# کیوان = U+06A9 U+06CC U+0648 U+0627 U+0646
# نصیری = U+0646 U+0635 U+06CC U+0631 U+06CC
$folder43Name = [char]0x06A9 + [char]0x06CC + [char]0x0648 + [char]0x0627 + [char]0x0646 + "_" + [char]0x0646 + [char]0x0635 + [char]0x06CC + [char]0x0631 + [char]0x06CC
$folder43 = Join-Path $workersPath $folder43Name
if (-not (Test-Path $folder43)) {
    New-Item -ItemType Directory -Path $folder43 -Force | Out-Null
}
$file43_4601 = $folder43Name + "_4601.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder43 $file43_4601) -Force

# Create مرجان_کرمی folder and files
# مرجان = U+0645 U+0631 U+062C U+0627 U+0646
# کرمی = U+06A9 U+0631 U+0645 U+06CC
$folder44Name = [char]0x0645 + [char]0x0631 + [char]0x062C + [char]0x0627 + [char]0x0646 + "_" + [char]0x06A9 + [char]0x0631 + [char]0x0645 + [char]0x06CC
$folder44 = Join-Path $workersPath $folder44Name
if (-not (Test-Path $folder44)) {
    New-Item -ItemType Directory -Path $folder44 -Force | Out-Null
}
$file44_4701 = $folder44Name + "_4701.jpg"
$file44_4702 = $folder44Name + "_4702.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder44 $file44_4701) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder44 $file44_4702) -Force

# Create اردشیر_میرزایی folder and files
# اردشیر = U+0627 U+0631 U+062F U+0634 U+06CC U+0631
# میرزایی = U+0645 U+06CC U+0631 U+0632 U+0627 U+06CC U+06CC
$folder45Name = [char]0x0627 + [char]0x0631 + [char]0x062F + [char]0x0634 + [char]0x06CC + [char]0x0631 + "_" + [char]0x0645 + [char]0x06CC + [char]0x0631 + [char]0x0632 + [char]0x0627 + [char]0x06CC + [char]0x06CC
$folder45 = Join-Path $workersPath $folder45Name
if (-not (Test-Path $folder45)) {
    New-Item -ItemType Directory -Path $folder45 -Force | Out-Null
}
$file45_4801 = $folder45Name + "_4801.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder45 $file45_4801) -Force

# Create شبنم_قربانی folder and files
# شبنم = U+0634 U+0628 U+0646 U+0645
# قربانی = U+0642 U+0631 U+0628 U+0627 U+0646 U+06CC
$folder46Name = [char]0x0634 + [char]0x0628 + [char]0x0646 + [char]0x0645 + "_" + [char]0x0642 + [char]0x0631 + [char]0x0628 + [char]0x0627 + [char]0x0646 + [char]0x06CC
$folder46 = Join-Path $workersPath $folder46Name
if (-not (Test-Path $folder46)) {
    New-Item -ItemType Directory -Path $folder46 -Force | Out-Null
}
$file46_4901 = $folder46Name + "_4901.jpg"
$file46_4902 = $folder46Name + "_4902.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder46 $file46_4901) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder46 $file46_4902) -Force

# Create رامین_یوسفی folder and files
# رامین = U+0631 U+0627 U+0645 U+06CC U+0646
# یوسفی = U+06CC U+0648 U+0633 U+0641 U+06CC
$folder47Name = [char]0x0631 + [char]0x0627 + [char]0x0645 + [char]0x06CC + [char]0x0646 + "_" + [char]0x06CC + [char]0x0648 + [char]0x0633 + [char]0x0641 + [char]0x06CC
$folder47 = Join-Path $workersPath $folder47Name
if (-not (Test-Path $folder47)) {
    New-Item -ItemType Directory -Path $folder47 -Force | Out-Null
}
$file47_5001 = $folder47Name + "_5001.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder47 $file47_5001) -Force

# Create سودابه_مهدوی folder and files
# سودابه = U+0633 U+0648 U+062F U+0627 U+0628 U+0647
# مهدوی = U+0645 U+0647 U+062F U+0648 U+06CC
$folder48Name = [char]0x0633 + [char]0x0648 + [char]0x062F + [char]0x0627 + [char]0x0628 + [char]0x0647 + "_" + [char]0x0645 + [char]0x0647 + [char]0x062F + [char]0x0648 + [char]0x06CC
$folder48 = Join-Path $workersPath $folder48Name
if (-not (Test-Path $folder48)) {
    New-Item -ItemType Directory -Path $folder48 -Force | Out-Null
}
$file48_5101 = $folder48Name + "_5101.jpg"
$file48_5102 = $folder48Name + "_5102.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder48 $file48_5101) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder48 $file48_5102) -Force

# Create بابک_طالبی folder and files
# بابک = U+0628 U+0627 U+0628 U+06A9
# طالبی = U+0637 U+0627 U+0644 U+0628 U+06CC
$folder49Name = [char]0x0628 + [char]0x0627 + [char]0x0628 + [char]0x06A9 + "_" + [char]0x0637 + [char]0x0627 + [char]0x0644 + [char]0x0628 + [char]0x06CC
$folder49 = Join-Path $workersPath $folder49Name
if (-not (Test-Path $folder49)) {
    New-Item -ItemType Directory -Path $folder49 -Force | Out-Null
}
$file49_5201 = $folder49Name + "_5201.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder49 $file49_5201) -Force

# Create گلناز_احمدی folder and files
# گلناز = U+06AF U+0644 U+0646 U+0627 U+0632
# احمدی = U+0627 U+062D U+0645 U+062F U+06CC
$folder50Name = [char]0x06AF + [char]0x0644 + [char]0x0646 + [char]0x0627 + [char]0x0632 + "_" + [char]0x0627 + [char]0x062D + [char]0x0645 + [char]0x062F + [char]0x06CC
$folder50 = Join-Path $workersPath $folder50Name
if (-not (Test-Path $folder50)) {
    New-Item -ItemType Directory -Path $folder50 -Force | Out-Null
}
$file50_5301 = $folder50Name + "_5301.jpg"
$file50_5302 = $folder50Name + "_5302.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder50 $file50_5301) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder50 $file50_5302) -Force

# Create تورج_موسوی folder and files
# تورج = U+062A U+0648 U+0631 U+062C
# موسوی = U+0645 U+0648 U+0633 U+0648 U+06CC
$folder51Name = [char]0x062A + [char]0x0648 + [char]0x0631 + [char]0x062C + "_" + [char]0x0645 + [char]0x0648 + [char]0x0633 + [char]0x0648 + [char]0x06CC
$folder51 = Join-Path $workersPath $folder51Name
if (-not (Test-Path $folder51)) {
    New-Item -ItemType Directory -Path $folder51 -Force | Out-Null
}
$file51_5401 = $folder51Name + "_5401.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder51 $file51_5401) -Force

# Create آتوسا_شاهینی folder and files
# آتوسا = U+0622 U+062A U+0648 U+0633 U+0627
# شاهینی = U+0634 U+0627 U+0647 U+06CC U+0646 U+06CC
$folder52Name = [char]0x0622 + [char]0x062A + [char]0x0648 + [char]0x0633 + [char]0x0627 + "_" + [char]0x0634 + [char]0x0627 + [char]0x0647 + [char]0x06CC + [char]0x0646 + [char]0x06CC
$folder52 = Join-Path $workersPath $folder52Name
if (-not (Test-Path $folder52)) {
    New-Item -ItemType Directory -Path $folder52 -Force | Out-Null
}
$file52_5501 = $folder52Name + "_5501.jpg"
$file52_5502 = $folder52Name + "_5502.jpg"
Copy-Item $sourceImg -Destination (Join-Path $folder52 $file52_5501) -Force
Copy-Item $sourceImg -Destination (Join-Path $folder52 $file52_5502) -Force

Write-Host "Persian folders created successfully"

