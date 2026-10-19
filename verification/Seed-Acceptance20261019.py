"""Populate an empty, explicitly initialized acceptance DB. Never edits launch settings.
Usage: python verification/Seed-Acceptance20261019.py <new.sqlite> <private-request.json>
All people/addresses/prices are synthetic acceptance fixtures, not real listings.
"""
from pathlib import Path
import sqlite3, json, sys, uuid, calendar, csv, hashlib
from datetime import date, datetime, timedelta, timezone
import bcrypt
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
path=Path(sys.argv[1]).resolve(); cfg=json.loads(Path(sys.argv[2]).read_text(encoding='utf-8-sig'))
folder=path.parent; now=datetime.now(timezone.utc); stamp=now.isoformat(); today=now.astimezone(timezone(timedelta(hours=7))).date()
db=sqlite3.connect(path); db.execute('PRAGMA foreign_keys=ON')
assert db.execute('SELECT COUNT(*) FROM tai_khoan').fetchone()[0]==0,'Only empty acceptance DB allowed'
def put(table,**values):
    keys=','.join(values); marks=','.join('?' for _ in values)
    return db.execute(f'INSERT INTO {table}({keys}) VALUES({marks})',list(values.values())).lastrowid
def uid():return uuid.uuid4().hex
access=[]; hashes={}
def account(role,name,email,password):
    if password not in hashes:hashes[password]=bcrypt.hashpw(password.encode(),bcrypt.gensalt(rounds=11)).decode()
    i=put('tai_khoan',ho_ten=name,email=email,so_dien_thoai=f'09{len(access)+10000000:08d}',mat_khau=hashes[password],vai_tro=role,dang_hoat_dong=1,email_confirmed=1,must_change_password=0,ngay_tao=stamp,ngay_cap_nhat=stamp)
    access.append(dict(id=i,role=role,name=name,email=email,password=password,buildings='',room='',contract=''))
    return i
def primary(key,role):return account(role,**cfg[key])
owner=primary('owner','CHU_NHA'); tenant=primary('tenant','KHACH_THUE'); manager=primary('manager','QUAN_LY'); admin=primary('admin','ADMIN')
owners=[owner]+[account('CHU_NHA',n,f'chunha{i}@nhatot.example.test',f'NghiemThuChuNha{i}2026!') for i,n in enumerate(['Nguyễn Minh Đức','Trần Thu Hà','Lê Quang Huy','Phạm Ngọc Lan'],2)]
managers=[manager]+[account('QUAN_LY',n,f'quanly{i}@nhatot.example.test',f'NghiemThuQuanLy{i}2026!') for i,n in enumerate(['Nguyễn Hải Nam','Trần Bảo Ngọc','Lê Anh Tuấn'],2)]
lasts=['Nguyễn','Trần','Lê','Phạm','Hoàng','Vũ','Đặng','Bùi','Đỗ','Ngô','Dương','Đinh']
middles=['Minh','Thu','Quang','Ngọc','Gia','Thanh','Đức','Hải','Bảo','Trường']
firsts=['Anh','Linh','Huy','Trang','Nam','Thảo','Tuấn','Hà','Long','Ngân','Khang','Yến','Dũng','Mai','Phúc','Vy','Sơn','Chi','Hiếu','Quỳnh','Tùng','Hương','Đạt','Nhi','Khánh']
def name(i):return f'{lasts[i%12]} {middles[(i//12)%10]} {firsts[(i//120+i%25)%25]}'
tenants=[tenant]+[account('KHACH_THUE',name(i),f'{i:03d}.khachthue@nhatot.example.test',f'KhachThue{i:03d}2026!') for i in range(2,301)]
front=back=None
for p in (ROOT/'QL_PhongTro/App_Data/identity-images').glob('*.jpg'):
    with Image.open(p) as im:
        if im.size==(582,343) and front is None:front=p.name
        if im.size==(447,266) and back is None:back=p.name
    if front and back:break
assert front and back,'Blank identity templates required'
profiles=[]
for n,a in enumerate(tenants):
    row=next(x for x in access if x['id']==a)
    profiles.append(put('khach_thue',tai_khoan_id=a,ho_ten=row['name'],ngay_sinh=f'{1995+n%10}-{1+n%12:02d}-{1+n%27:02d}',so_giay_to=f'000000{n+1:06d}',giay_to_bon_so_cuoi=f'{n+1:04d}',anh_giay_to_truoc=front,anh_giay_to_sau=back,dia_chi_thuong_tru=['Thành phố Thái Nguyên','Thành phố Bắc Ninh','Huyện Đông Anh, Hà Nội','Thành phố Hải Dương'][n%4],nghe_nghiep=['Lập trình viên','Sinh viên','Nhân viên văn phòng','Kế toán','Kỹ thuật viên'][n%5],so_dien_thoai=f'09{access.index(row)+10000000:08d}',email=row['email'],ngay_tao=stamp))
buildings=[('An Phú','18 ngõ 165 Xuân Thủy','Dịch Vọng Hậu','Cầu Giấy',3300000),('Trúc Xanh','26 ngõ 68 Cầu Giấy','Quan Hoa','Cầu Giấy',3100000),('Bình An','35 ngõ 43 Trung Kính','Trung Hòa','Cầu Giấy',3800000),('Hoa Sữa','12 ngõ 44 Trần Thái Tông','Dịch Vọng','Cầu Giấy',3700000),('Ánh Dương','28 ngõ 121 Thái Hà','Trung Liệt','Đống Đa',3500000),('Ngọc Khánh','16 ngõ 20 Nguyễn Chí Thanh','Láng Thượng','Đống Đa',3900000),('Hồng Hà','42 ngõ 122 Vĩnh Tuy','Vĩnh Tuy','Hai Bà Trưng',3000000),('Thanh Xuân','21 ngõ 63 Lê Văn Lương','Nhân Chính','Thanh Xuân',3600000),('Hà Đông','14 ngõ 6 Nguyễn Văn Lộc','Mộ Lao','Hà Đông',2700000),('Tây Hồ','32 ngõ 28 Xuân La','Xuân La','Tây Hồ',4100000)]
services=[]
for code,title,mode,unit,price in [('DIEN','Điện','THEO_CHI_SO','kWh',3800),('NUOC','Nước','THEO_CHI_SO','m³',22000),('INTERNET','Internet','CO_DINH','tháng',100000),('RAC','Vệ sinh','THEO_NGUOI','người',30000),('GUI_XE','Gửi xe','THEO_NGUOI','người',80000)]:
    sid=put('dich_vu',ma_dich_vu=code,ten_dich_vu=title,dang_hoat_dong=1);services.append((sid,title,mode,unit,price))
# Reuse the supplied room photographs already present locally; copy to unique per-room names.
photos={}
for p in (ROOT/'QL_PhongTro/wwwroot/uploads/rooms').rglob('*.jpg'):
    if '-thumb' in p.name:continue
    with Image.open(p) as im:
        if im.size not in photos:photos[im.size]=p
photo_list=list(photos.values());assert len(photo_list)>=8
cases=[]; contract_number=0
with db:
 for b,(title,street,ward,district,base) in enumerate(buildings,1):
    own=owners[(b-1)//2]; mgr=managers[0 if b<=2 else 1+(b-3)%3]
    put('toa_nha',id=b,chu_nha_id=own,quan_ly_id=mgr,ten_toa_nha=f'Nhà trọ {title}',dia_chi=street,phuong_xa=ward,quan_huyen=district,tinh_thanh='Hà Nội',so_tang=6,ngay_chot_hang_thang=30,bai_do_xe=1,camera_an_ninh=1,khu_giat_say=1,san_thuong=1,ghi_chu='Địa chỉ và thông tin giả lập phục vụ nghiệm thu.')
    for a in access:
        if a['id'] in (own,mgr):a['buildings']+=(' ; ' if a['buildings'] else '')+f'{b}. Nhà trọ {title}'
    conf=[]
    for sid,sname,mode,unit,price in services:
        cid=put('cau_hinh_dich_vu',toa_nha_id=b,dich_vu_id=sid,cach_tinh=mode,don_vi_tinh=unit,don_gia=price,tu_ngay='2026-01-01',nguoi_tao_id=own,ngay_tao='2026-01-01T01:00:00Z')
        ds=put('dich_vu_toa_nha',toa_nha_id=b,dich_vu_id=sid,ap_dung_mac_dinh=1);conf.append((sid,sname,mode,unit,price,cid,ds))
    for j in range(48):
        rid=(b-1)*48+j+1; floor=j//8+1; code=f'{floor}{j%8+1:02d}'; price=base+(j%6)*150000; occupied=j<28
        state='DANG_THUE' if occupied else ('NGUNG_CHO_THUE' if j==47 else 'TRONG')
        room=put('phong_tro',id=rid,toa_nha_id=b,ma_phong=code,tang=floor,loai_phong='Căn hộ mini' if j%3==0 else 'Phòng có gác',dien_tich=22+j%15,gia_thue=price,tien_coc_du_kien=price,so_nguoi_toi_da=3,trang_thai=state,mo_ta=f'Phòng {code} tại {street}, {ward}, {district}. Diện tích {22+j%15} m², cửa sổ thoáng, bếp và vệ sinh riêng; có điều hòa, nóng lạnh, chỗ để xe và khóa cổng riêng. Giờ giấc tự do, không chung chủ.',tien_nghi=json.dumps(['Điều hòa','Nóng lạnh','Kệ bếp','Wifi'],ensure_ascii=False),ngay_tao='2026-01-01T01:00:00Z')
        out=ROOT/'QL_PhongTro/wwwroot/uploads/rooms'/str(room);out.mkdir(parents=True,exist_ok=True)
        for k in range(3):
            source=photo_list[(rid+k)%len(photo_list)]; key=uid();target=out/(key+'.jpg');thumb=out/(key+'-thumb.jpg')
            with Image.open(source) as im:
                im.convert('RGB').save(target,quality=90);im.thumbnail((400,400));im.convert('RGB').save(thumb,quality=82)
            put('anh_phong',phong_id=room,duong_dan=f'/uploads/rooms/{room}/{target.name}',duong_dan_anh_nho=f'/uploads/rooms/{room}/{thumb.name}',thu_tu=k+1,mo_ta=['Không gian phòng','Góc sinh hoạt','Nội thất tham khảo'][k],ngay_tao=stamp)
        for sid,sname,mode,unit,rate,cid,ds in conf:put('dich_vu_phong',phong_id=room,dich_vu_toa_nha_id=ds)
        listing_state='DA_CHO_THUE' if occupied else ('NHAP' if j>=43 else 'DANG_HIEN_THI')
        listing=put('tin_dang',phong_id=room,nguoi_dang_id=own,tieu_de=f'{"Căn hộ mini" if j%3==0 else "Phòng có gác"} {22+j%15}m² tại {street.split(" ",1)[1]}, {district} – phòng {code}',noi_dung=f'Cho thuê phòng sạch đẹp tại Nhà trọ {title}, {street}, {ward}, {district}, Hà Nội.\nGiá {price:,} đồng/tháng, cọc một tháng. Có điều hòa, nóng lạnh, bếp riêng, vệ sinh khép kín. Internet 100.000đ/phòng; điện 3.800đ/kWh, nước 22.000đ/m³ theo công tơ riêng. Gửi xe 80.000đ/người, vệ sinh 30.000đ/người.\nLiên hệ quản lý đặt lịch xem phòng. Hình ảnh minh họa và nội dung giả lập cho nghiệm thu.',trang_thai=listing_state,ngay_dang=stamp if listing_state=='DANG_HIEN_THI' else None,ngay_het_han=(now+timedelta(days=30)).isoformat() if listing_state=='DANG_HIEN_THI' else None,ngay_tao=stamp)
        if not occupied:
            put('lich_su_trang_thai_phong',phong_id=room,trang_thai=state,tu_thoi_diem='2026-01-01T00:00:00Z')
            continue
        contract_number+=1; n=contract_number-1; aid=tenants[n]; profile=profiles[n]; people=2 if j%4==0 else 1
        contract=put('hop_dong',ma_hop_dong=f'HD-2026-{contract_number:04d}',phong_id=room,khach_dung_ten_id=profile,trang_thai='DANG_HIEU_LUC',tien_coc_thoa_thuan=price,ngay_chot_hang_thang=30,nguoi_lap_id=own,ngay_tao='2026-06-28T03:00:00Z')
        ky=put('ky_hop_dong',hop_dong_id=contract,so_thu_tu=1,ngay_bat_dau='2026-07-01',ngay_ket_thuc='2027-06-30',so_thang=12,gia_thue=price,nguoi_lap_id=own,ngay_tao='2026-06-28T03:00:00Z')
        a=next(x for x in access if x['id']==aid);a.update(buildings=f'{b}. Nhà trọ {title}',room=code,contract=f'HD-2026-{contract_number:04d}')
        if people==2:
            pi=put('khach_thue',ho_ten=name(500+n),ngay_sinh='2001-05-12',so_giay_to=f'000001{n+1:06d}',giay_to_bon_so_cuoi=f'{n+1:04d}',anh_giay_to_truoc=front,anh_giay_to_sau=back,nghe_nghiep='Nhân viên văn phòng',ngay_tao=stamp)
            put('nguoi_o_ghep',hop_dong_id=contract,khach_thue_id=pi,ngay_vao='2026-07-01')
        put('hop_dong_chi_so_dau_ky',hop_dong_id=contract,ngay_ban_giao='2026-07-01',chi_so_dien=str(1000+rid*7),chi_so_nuoc=str(100+rid),nguoi_nhap_id=mgr,ngay_nhap='2026-07-01T01:00:00Z')
        put('lich_su_trang_thai_phong',phong_id=room,trang_thai='TRONG',tu_thoi_diem='2026-01-01T00:00:00Z',den_thoi_diem='2026-06-30T17:00:00Z')
        put('lich_su_trang_thai_phong',phong_id=room,hop_dong_id=contract,trang_thai='DANG_THUE',tu_thoi_diem='2026-06-30T17:00:00Z')
        put('giao_dich_coc',ma_giao_dich_coc=f'COC-2026-{contract:04d}',khoa_chong_trung=uid(),hop_dong_id=contract,loai_giao_dich='THU_COC',so_tien=price,hinh_thuc='CHUYEN_KHOAN',trang_thai='DA_XAC_NHAN',nguoi_thuc_hien_id=own,ngay_giao_dich='2026-06-28T03:00:00Z')
        for sid,sname,mode,unit,rate,cid,ds in conf:put('hop_dong_dich_vu',hop_dong_id=contract,dich_vu_id=sid,cau_hinh_dich_vu_id=cid,ten_dich_vu=sname,cach_tinh=mode,don_vi_tinh=unit,don_gia=rate,ngay_ghi_nhan='2026-06-28T03:00:00Z')
        readings={services[0][0]:1000+rid*7,services[1][0]:100+rid}
        for month in (7,8,9):
            begin=date(2026,month,1);end=date(2026,month,calendar.monthrange(2026,month)[1]);issue=end+timedelta(days=1);due=issue+timedelta(days=7)
            total=price; lines=[dict(loai_khoan='TIEN_PHONG',ten_khoan='Tiền phòng',don_vi_tinh='tháng',so_luong='1',don_gia=price,thanh_tien=price,ky_hop_dong_id=ky)]
            for sid,sname,mode,unit,rate,cid,ds in conf:
                qty=(65+rid%90+month*3 if sid==services[0][0] else 3+rid%7) if mode=='THEO_CHI_SO' else (people if mode=='THEO_NGUOI' else 1)
                line=dict(loai_khoan='DICH_VU',ten_khoan=sname,dich_vu_id=sid,cau_hinh_dich_vu_id=cid,cach_tinh_ap_dung=mode,don_vi_tinh=unit,so_luong=str(qty),don_gia=rate,thanh_tien=qty*rate)
                if mode=='THEO_CHI_SO':
                    old=readings[sid];new=old+qty;readings[sid]=new
                    cs=put('chi_so_dien_nuoc',hop_dong_id=contract,dich_vu_id=sid,tu_ngay=str(begin),den_ngay=str(end),chi_so_dau=str(old),chi_so_cuoi=str(new),nguoi_nhap_id=mgr,ngay_nhap=f'{end}T09:00:00Z')
                    line.update(chi_so_id=cs,chi_so_dau=str(old),chi_so_cuoi=str(new))
                total+=qty*rate;lines.append(line)
            if month==9 and j%7==2:due=today+timedelta(days=5)
            invoice=put('hoa_don',ma_hoa_don=f'HD-{b:02d}-{code}-2026{month:02d}',hop_dong_id=contract,thang=month,nam=2026,tu_ngay=str(begin),den_ngay=str(end),ngay_chot=str(end),so_nguoi_tinh_phi=people,ngay_lap=f'{issue}T01:00:00Z',han_thanh_toan=str(due),tong_tien=total,trang_thai='NHAP',nguoi_lap_id=own)
            for order,line in enumerate(lines,1):put('chi_tiet_hoa_don',hoa_don_id=invoice,so_thu_tu=order,**line)
            db.execute("UPDATE hoa_don SET trang_thai='DA_PHAT_HANH',ngay_phat_hanh=?,ngay_phat_hanh_nghiep_vu=?,nguoi_phat_hanh_id=? WHERE id=?",(f'{issue}T01:00:00Z',str(issue),own,invoice))
            db.execute('UPDATE chi_so_dien_nuoc SET da_khoa=1 WHERE id IN (SELECT chi_so_id FROM chi_tiet_hoa_don WHERE hoa_don_id=?)',(invoice,))
            # First test tenant: July paid, August partial, September awaiting confirmation.
            kind='DU' if j%7 in (3,4,5,6) else ('THIEU' if j%7==0 else 'CHUA_TRA')
            paid=total if (month==7 or (month==8 and j%7>=2) or kind=='DU') else (total//2 if kind=='THIEU' else 0)
            if paid:
                payment=put('thanh_toan',hoa_don_id=invoice,so_tien=paid,ngay_thanh_toan=str(issue+timedelta(days=3)),trang_thai='DA_XAC_NHAN')
                put('lan_thu_hoa_don',thanh_toan_id=payment,hinh_thuc='CHUYEN_KHOAN',nguoi_xac_nhan_id=own,ten_nguoi_xac_nhan=next(x['name'] for x in access if x['id']==own),thoi_diem_ghi=f'{issue+timedelta(days=3)}T02:00:00Z',khoa_chong_trung=uid())
            if month==9 and j%7==0:
                payment=put('thanh_toan',hoa_don_id=invoice,so_tien=total-paid,ngay_thanh_toan=str(today),trang_thai='CHO_XAC_NHAN',hinh_thuc_bao_cao='TIEN_MAT')
                put('bao_cao_thanh_toan',thanh_toan_id=payment,nguoi_gui_id=aid,gui_luc=stamp,ghi_chu='Báo nộp tiền mặt để chủ nhà đối chiếu.')
                put('thong_bao_thanh_toan',thanh_toan_id=payment,nguoi_nhan_id=own,loai='BAO_MOI',tieu_de='Báo thanh toán mới',noi_dung=f'{a["name"]} báo thanh toán phòng {code}, Nhà trọ {title}.',duong_dan=f'/HoaDonDichVu/Details/{invoice}#payment-report-{payment}',ngay_tao=stamp)
            put('thong_bao',nguoi_nhan_id=aid,hoa_don_id=invoice,loai_thong_bao='HOA_DON_MOI',tieu_de=f'Hóa đơn tháng {month}/2026',noi_dung=f'Hóa đơn phòng {code}: tổng {total:,} đồng, đã thu {paid:,} đồng, còn {total-paid:,} đồng; hạn {due:%d/%m/%Y}.',duong_dan=f'/ThongBao/HoaDon/{invoice}',email_nhan=a['email'],ngay_tao=f'{issue}T01:00:00Z',trang_thai_email='DA_HUY',loi_gan_nhat='Dữ liệu nghiệm thu lịch sử; không gửi email thật.')
            cases.append(dict(building=title,room=code,contract=contract,invoice=invoice,month=month,total=total,paid=paid,debt=total-paid,due=str(due),email=a['email']))
        if j<5:
            state=['MOI','DANG_XU_LY','HOAN_THANH','TU_CHOI','MOI'][j]
            report=put('bao_hong',ma_bao_hong=f'BH-{today:%Y%m}-{(b-1)*5+j+1:04d}',nguoi_bao_id=aid,khach_thue_id=profile,phong_id=room,hop_dong_id=contract,loai_thiet_bi=['Vòi nước','Điều hòa','Bóng đèn','Ổ cắm','Bình nóng lạnh'][j],mo_ta=['Vòi lavabo bị rò nước tại đầu nối, sàn thường xuyên ướt.','Điều hòa chạy nhưng không lạnh, có tiếng kêu.','Đèn trần không sáng, cần kiểm tra thay bóng.','Ổ cắm cạnh cửa bị lỏng, cần kiểm tra.','Bình nóng lạnh không lên nguồn.'][j],muc_do='GAP' if j in (0,4) else 'THUONG',trang_thai=state,ngay_tao=(now-timedelta(hours=30+j)).isoformat(),khoa_chong_trung=uid())
            if state!='MOI':put('bao_hong_trao_doi',bao_hong_id=report,nguoi_gui_id=mgr,ten_nguoi_gui=next(x['name'] for x in access if x['id']==mgr),loai='PHAN_HOI',noi_dung='Đã tiếp nhận thông tin, kiểm tra thiết bị và trao đổi lịch xử lý với khách.',thoi_diem=stamp,khoa_chong_trung=uid())
        put('nhat_ky_hoat_dong',nguoi_thuc_hien_id=own,ten_nguoi_thuc_hien=next(x['name'] for x in access if x['id']==own),vai_tro_luc_thuc_hien='CHU_NHA',loai_doi_tuong='hop_dong',doi_tuong_id=contract,hanh_dong='TAO',du_lieu_sau=json.dumps({'ma_hop_dong':a['contract'],'trang_thai':'DANG_HIEU_LUC'},ensure_ascii=False),thoi_diem='2026-06-28T03:00:00Z')
 for index in range(20):
    b=index//2+1; room=(b-1)*48+29+index%2
    listing=db.execute('SELECT id FROM tin_dang WHERE phong_id=?',(room,)).fetchone()[0]
    state='MOI' if index%2==0 else 'DA_HEN_LICH'
    req=put('yeu_cau_thue',ma_yeu_cau=f'YC-{today:%Y%m}-{index+1:04d}',tin_dang_id=listing,khach_thue_id=profiles[280+index],loai_yeu_cau='XEM_PHONG',ngay_mong_muon=str(today+timedelta(days=2)),so_nguoi_du_kien=2,loi_nhan='Tôi muốn xem phòng sau giờ làm, ưu tiên phòng thoáng và có chỗ để xe.',lich_hen=(now+timedelta(days=2)).isoformat() if state=='DA_HEN_LICH' else None,trang_thai=state,ngay_tao=stamp)
    put('yeu_cau_thue_lich_su',yeu_cau_thue_id=req,trang_thai_moi='MOI',hanh_dong='TAO',nguoi_thuc_hien_id=tenants[280+index],ten_nguoi_thuc_hien=next(x['name'] for x in access if x['id']==tenants[280+index]),vai_tro_luc_thuc_hien='KHACH_THUE',thoi_diem=stamp)
 put('bao_hong_so_ma',thang=today.strftime('%Y%m'),so_cuoi=50)
 db.execute('INSERT INTO hop_dong_so_ma(nam,so_cuoi) VALUES(2026,280)')
 assert db.execute('PRAGMA integrity_check').fetchone()[0]=='ok'
 assert not db.execute('PRAGMA foreign_key_check').fetchall()
db.close()
with (folder/'tai-khoan.csv').open('w',encoding='utf-8-sig',newline='') as f:
    writer=csv.DictWriter(f,fieldnames=list(access[0]));writer.writeheader();writer.writerows(access)
(folder/'access.json').write_text(json.dumps(access,ensure_ascii=False,indent=2),encoding='utf-8')
with (folder/'hoa-don-doi-chieu.csv').open('w',encoding='utf-8-sig',newline='') as f:
    writer=csv.DictWriter(f,fieldnames=list(cases[0]));writer.writeheader();writer.writerows(cases)
print(json.dumps({'buildings':10,'rooms':480,'contracts':contract_number,'invoices':len(cases),'accounts':len(access),'roomPhotos':1440,'debt':sum(c['debt'] for c in cases)},ensure_ascii=True))
