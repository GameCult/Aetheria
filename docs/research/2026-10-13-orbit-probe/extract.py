import sys,os,re
log,outd=sys.argv[1],sys.argv[2]; os.makedirs(outd,exist_ok=True)
cur=None;f=None
for line in open(log,encoding='utf-8',errors='replace'):
    line=line.rstrip('\r\n')
    m=re.match(r'@@FILE (\w+)',line)
    if m:
        if f:f.close()
        cur=m.group(1);f=open(os.path.join(outd,cur+'.csv'),'w',encoding='utf-8');continue
    if line.startswith('@@END'):
        if f:f.close()
        f=None;cur=None;continue
    if f: f.write(line+'\n')
