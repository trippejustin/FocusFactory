using UnityEngine;
using UnityEngine.UI;

public class TrialSelectionController : MonoBehaviour
{
    [SerializeField] private Button[] spatialButtons;
    [SerializeField] private Button[] conventionalButtons;

    private void Start()
    {
        int run = StudySessionManager.Instance.CurrentRun;

        for (int i = 0; i < 3; i++)
        {
            spatialButtons[i].interactable = false;
            conventionalButtons[i].interactable = false;
        }

        if (StudySessionManager.Instance.IsStudyComplete())
            return;

        int row = run / 2;

        if (run % 2 == 0)
        {
            spatialButtons[row].interactable = true;
            conventionalButtons[row].interactable = true;
        }
        else
        {
            var mode = StudySessionManager.Instance.CurrentInterface;

            spatialButtons[row].interactable =
                mode == StudySessionManager.InterfaceStyle.Spatial;

            conventionalButtons[row].interactable =
                mode == StudySessionManager.InterfaceStyle.Conventional;
        }
    }

    public void SelectSpatial(int trial)
    {
        StudySessionManager.Instance.ChooseInterface(
            StudySessionManager.InterfaceStyle.Spatial);

        SceneTransition.Instance.LoadScene("Trial" + trial);
    }

    public void SelectConventional(int trial)
    {
        StudySessionManager.Instance.ChooseInterface(
            StudySessionManager.InterfaceStyle.Conventional);

        SceneTransition.Instance.LoadScene("Trial" + trial);
    }
}