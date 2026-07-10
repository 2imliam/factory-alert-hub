# README - Huong dan su dung MyLogin va n8n Automation

Tai lieu nay huong dan cach su dung MyLogin ket hop voi n8n automation de tu dong lay du lieu san xuat, tinh toan muc dat/khong dat, va gui thong bao qua API theo lich.

## 1. Tong quan he thong

He thong gom 2 phan chinh:

- **MyLogin**: noi nhan thong bao hoac API dich de day noi dung canh bao/bao cao.
- **n8n Automation**: workflow tu dong chay theo lich, lay du lieu tu Google Sheets, xu ly logic va gui request den API.

Workflow trong n8n co cac node chinh:

1. **Cron Expression**: chay tu dong theo khung gio cai dat.
2. **phan loai gio**: xac dinh thoi diem hien tai thuoc ca/gio nao.
3. **Router**: tach nhanh xu ly theo dieu kien.
4. **Get All Rows**: lay du lieu tu Google Sheets.
5. **lay cot**: chon va chuan hoa cac cot can dung.
6. **Hourly production**: tinh san luong theo gio.
7. **Hourly target**: tinh muc tieu theo gio.
8. **true/false**: kiem tra dat hay khong dat.
9. **truyen dat**: tao noi dung thong bao.
10. **api**: tao payload gui sang MyLogin/API.
11. **Send HTTP request**: gui thong bao.

## 2. Chuan bi truoc khi dung

Can chuan bi:

- Tai khoan **n8n** da co quyen tao/sua workflow.
- Tai khoan **Google Sheets** co quyen doc file du lieu san xuat.
- API endpoint cua **MyLogin** hoac he thong nhan thong bao.
- Token/API key neu MyLogin yeu cau xac thuc.
- File Google Sheets co cau truc cot on dinh.

Vi du cac thong tin can co:

```env
MYLOGIN_API_URL=https://your-domain.com/api/notify
MYLOGIN_API_TOKEN=your_api_token
GOOGLE_SHEET_ID=your_google_sheet_id
SHEET_NAME=Sheet1
```

## 3. Cau hinh MyLogin

1. Dang nhap vao MyLogin.
2. Kiem tra tai khoan co quyen nhan/gui thong bao tu API.
3. Tao hoac lay thong tin API:
   - Endpoint URL.
   - Token/API key.
   - Dinh dang body ma API yeu cau.
4. Neu API co whitelist IP, them IP may chu dang chay n8n.
5. Test API bang Postman, curl hoac node HTTP Request trong n8n.

Vi du body gui thong bao:

```json
{
  "title": "Thong bao san xuat",
  "message": "Chuyen dat muc tieu gio hien tai",
  "status": "passed",
  "time": "2026-07-10 08:00"
}
```

## 4. Cau hinh Google Sheets

Google Sheets nen co cac cot ro rang, vi du:

| Cot | Noi dung |
| --- | --- |
| Ngay | Ngay san xuat |
| Gio | Khung gio san xuat |
| Chuyen | Ten chuyen/line |
| San_luong | San luong thuc te |
| Muc_tieu | Muc tieu theo gio |
| Trang_thai | Ghi chu neu co |

Luu y:

- Ten cot khong nen thay doi tuy tien sau khi workflow da hoat dong.
- Du lieu san luong va muc tieu nen la so.
- Neu co nhieu sheet, can dung dung ten sheet trong node Google Sheets.

## 5. Cau hinh workflow n8n

### Buoc 1: Cron Expression

Node nay dung de hen gio workflow tu chay.

Vi du chay moi gio:

```cron
0 * * * *
```

Vi du chay moi 30 phut:

```cron
*/30 * * * *
```

Nen chon timezone phu hop voi nha may, vi du:

```text
Asia/Ho_Chi_Minh
```

### Buoc 2: phan loai gio

Node Code nay dung de xac dinh khung gio hien tai.

Muc dich:

- Lay gio hien tai.
- Gan vao ca san xuat hoac gio san xuat.
- Quyet dinh co tiep tuc workflow hay khong.

Ket qua dau ra nen co cac truong:

```json
{
  "currentHour": 8,
  "shift": "Ca 1",
  "shouldRun": true
}
```

### Buoc 3: Router

Router tach nhanh xu ly:

- Nhanh chinh: workflow tiep tuc khi dung khung gio can bao cao.
- Nhanh Otherwise: ket thuc neu khong dung dieu kien.

Dieu kien goi y:

```text
shouldRun = true
```

### Buoc 4: Get All Rows

Node Google Sheets lay toan bo du lieu tu file san xuat.

Can cau hinh:

- Credential Google Sheets.
- Spreadsheet ID.
- Sheet name.
- Range neu can gioi han du lieu.

Nen test node nay truoc de dam bao n8n doc duoc du lieu.

### Buoc 5: lay cot

Node Code dung de lay dung cac cot can xu ly.

Muc dich:

- Bo cot thua.
- Doi ten field neu can.
- Chuan hoa so lieu.

Vi du output:

```json
{
  "line": "Line 1",
  "hour": "08:00-09:00",
  "production": 120,
  "target": 100
}
```

### Buoc 6: Hourly production

Node nay tinh tong san luong theo gio hien tai.

Kiem tra:

- San luong co dung kieu so khong.
- Co lay dung khung gio hien tai khong.
- Neu co nhieu chuyen/line, can group theo chuyen/line.

### Buoc 7: Hourly target

Node nay tinh muc tieu can dat trong gio.

Quy tac co the la:

- Lay truc tiep tu cot `Muc_tieu`.
- Hoac tinh theo cong thuc rieng.
- Hoac lay tu bang ke hoach san xuat.

### Buoc 8: true/false

Node nay so sanh san luong thuc te voi muc tieu.

Logic goi y:

```js
const passed = production >= target;
return {
  passed,
  production,
  target,
  diff: production - target
};
```

Ket qua:

- `passed = true`: dat muc tieu.
- `passed = false`: chua dat muc tieu.

### Buoc 9: truyen dat

Node nay tao noi dung thong bao de gui di.

Vi du thong bao dat:

```text
Line 1 da dat muc tieu gio 08:00-09:00.
San luong: 120/100.
```

Vi du thong bao chua dat:

```text
Line 1 chua dat muc tieu gio 08:00-09:00.
San luong: 80/100. Thieu 20.
```

### Buoc 10: api

Node nay tao payload dung dinh dang API MyLogin.

Vi du:

```json
{
  "title": "Bao cao san xuat theo gio",
  "message": "Line 1 da dat muc tieu gio 08:00-09:00. San luong: 120/100.",
  "status": "passed",
  "line": "Line 1",
  "production": 120,
  "target": 100
}
```

### Buoc 11: Send HTTP request

Node HTTP Request gui payload sang MyLogin.

Cau hinh goi y:

- Method: `POST`
- URL: `MYLOGIN_API_URL`
- Authentication: Bearer Token hoac Header tuy API
- Header:

```http
Content-Type: application/json
Authorization: Bearer YOUR_TOKEN
```

- Body: JSON tu node `api`

Sau khi cau hinh, bam **Execute Step** de test truoc khi active workflow.

## 6. Cach van hanh hang ngay

1. Kiem tra Google Sheets da co du lieu moi.
2. Mo n8n va kiem tra workflow dang o trang thai **Active**.
3. Kiem tra node Cron co dung lich chay.
4. Theo doi execution history trong n8n.
5. Kiem tra MyLogin co nhan thong bao dung noi dung khong.

Neu can chay thu cong:

1. Mo workflow trong n8n.
2. Bam **Execute Workflow**.
3. Xem output tung node.
4. Sua loi neu node nao bao do.

## 7. Kiem tra sau khi cai dat

Checklist:

- [ ] Cron chay dung gio.
- [ ] Router di dung nhanh.
- [ ] Google Sheets doc duoc du lieu.
- [ ] Cac node Code tra ve dung field.
- [ ] San luong va muc tieu tinh dung.
- [ ] Ket qua true/false dung voi thuc te.
- [ ] Payload API dung dinh dang.
- [ ] HTTP Request tra ve status thanh cong.
- [ ] MyLogin hien thong bao dung noi dung.

## 8. Loi thuong gap va cach xu ly

### Workflow khong tu chay

- Kiem tra workflow da bat **Active** chua.
- Kiem tra timezone cua Cron.
- Kiem tra Cron Expression co dung khong.

### Khong doc duoc Google Sheets

- Kiem tra credential Google Sheets.
- Kiem tra tai khoan co quyen doc file.
- Kiem tra Spreadsheet ID va Sheet name.
- Kiem tra file co bi doi ten cot khong.

### Tinh sai san luong hoac muc tieu

- Kiem tra du lieu dau vao co phai dang so khong.
- Kiem tra filter theo gio/ca.
- Kiem tra cong thuc trong node Code.

### MyLogin khong nhan thong bao

- Kiem tra API URL.
- Kiem tra token/API key.
- Kiem tra header `Content-Type`.
- Kiem tra body JSON co dung dinh dang API yeu cau khong.
- Xem response cua node HTTP Request.

### HTTP Request bao loi 401/403

- Token sai hoac het han.
- Tai khoan khong co quyen.
- IP cua n8n chua duoc cho phep.

### HTTP Request bao loi 400

- Body gui len sai field.
- Thieu truong bat buoc.
- Sai kieu du lieu, vi du gui chuoi thay vi so.

## 9. Nguyen tac sua workflow

- Chi sua tung node va test lai node do.
- Truoc khi sua lon, nen duplicate workflow de backup.
- Khong doi ten cot trong Google Sheets neu chua sua lai node Code.
- Sau khi sua API payload, phai test lai node HTTP Request.
- Nen ghi chu version workflow khi co thay doi quan trong.

## 10. Goi y cau truc backup

Nen luu:

- File export workflow n8n dang `.json`.
- Anh chup man hinh workflow.
- Mau Google Sheets.
- Thong tin API endpoint va cac field bat buoc.

Thu muc goi y:

```text
backup/
  n8n-workflow-YYYY-MM-DD.json
  google-sheet-template.xlsx
  api-payload-example.json
```

## 11. Quy trinh test nhanh

1. Chay node **Cron Expression** hoac bam **Execute Workflow**.
2. Kiem tra output node **phan loai gio**.
3. Kiem tra Router co vao nhanh chinh khong.
4. Kiem tra node **Get All Rows** co du lieu.
5. Kiem tra output node **true/false**.
6. Kiem tra payload node **api**.
7. Chay node **Send HTTP request**.
8. Xac nhan thong bao da xuat hien trong MyLogin.

## 12. Thong tin can cap nhat khi trien khai thuc te

Thay cac gia tri mau bang thong tin that:

- `MYLOGIN_API_URL`
- `MYLOGIN_API_TOKEN`
- `GOOGLE_SHEET_ID`
- `SHEET_NAME`
- Ten cot trong Google Sheets
- Cong thuc tinh san luong
- Cong thuc tinh muc tieu
- Noi dung thong bao mong muon

---

Neu workflow da gui thong bao thanh cong va execution history khong co loi, he thong co the de chay tu dong theo lich.
