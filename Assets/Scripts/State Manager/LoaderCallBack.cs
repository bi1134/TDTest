using UnityEngine;

public class LoaderCallBack : MonoBehaviour
{
    private bool isFirstUpdates = true;

    private void Update()
    {
        if (isFirstUpdates)
        {
            isFirstUpdates = false;
            Loader.LoaderCallback();
        }
    }
}
