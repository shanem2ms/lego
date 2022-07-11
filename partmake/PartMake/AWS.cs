using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using System.Diagnostics;

namespace partmake
{
    // Access key ID: AKIAXPMVBCNZ65QWFKPX
    // Secret access key: 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
    public class AWS
    {
        IAmazonS3 s3client;
        public AWS()
        {
            BasicAWSCredentials awsCreds = new BasicAWSCredentials("AKIAXPMVBCNZ7DYXXOWF", "UgpdE0N3mnsHs6sFwMsa9by8Ua5LGLhS4MlEmZOd");
            AmazonS3Config config = new AmazonS3Config();
            s3client = new AmazonS3Client(awsCreds, Amazon.RegionEndpoint.USEast2);
        }

        public async Task<bool> DoWork()
        {
            var results= await s3client.ListBucketsAsync();
            foreach (var bucket in results.Buckets)
            {

            }
            return true;
        }
    }
}
