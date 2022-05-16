copy /Y ldraw\LDConfig.ldr cache\
cd cache
7z a cache.zip *
aws s3api put-object --bucket mybricks --key cache.zip --body cache.zip