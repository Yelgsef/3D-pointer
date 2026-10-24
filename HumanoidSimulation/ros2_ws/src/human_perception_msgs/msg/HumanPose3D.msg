std_msgs/Header header
# Parallel arrays share indices; header.frame_id is the metric point frame.
string[] keypoint_names
geometry_msgs/Point[] keypoints
float32[] confidences
bool[] valid
