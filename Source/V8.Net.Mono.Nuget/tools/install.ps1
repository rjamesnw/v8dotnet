param($installPath, $toolsPath, $package, $project)

$OpencvFiles = @(
	"emgulibcvextern.so";
	"libopencv_calib3d.so";
	"libopencv_contrib.so";
	"libopencv_core.so";
	"libopencv_cudaarithm.so";
	"libopencv_cudabgsegm.so";
	"libopencv_cudacodec.so";
	"libopencv_cudafeatures2d.so";
	"libopencv_cudafilters.so";
	"libopencv_cudaimgproc.so";
	"libopencv_cudaoptflow.so";
	"libopencv_cuda.so";
	"libopencv_cudastereo.so";
	"libopencv_cudawarping.so";
	"libopencv_features2d.so";
	"libopencv_flann.so";
	"libopencv_highgui.so";
	"libopencv_imgproc.so";
	"libopencv_legacy.so";
	"libopencv_ml.so";
	"libopencv_nonfree.so";
	"libopencv_objdetect.so";
	"libopencv_optim.so";
	"libopencv_photo.so";
	"libopencv_shape.so";
	"libopencv_softcascade.so";
	"libopencv_stitching.so";
	"libopencv_superres.so";
	"libopencv_video.so";
	"libopencv_videostab.so";

)

# set 'Copy To Output Directory' to 'Copy if newer'
foreach ( $file in $OpencvFiles )
{
	$project.ProjectItems.Item($file).Properties.Item("CopyToOutputDirectory").Value = 1
}



  

