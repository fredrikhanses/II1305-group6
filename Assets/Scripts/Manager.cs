﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
public class Manager : MonoBehaviour {
    public GameOverScreen go;
    public void changeScene(int Scene)
    {
        
            SceneManager.LoadScene(Scene);

    }
    public void resume()
    {
        Time.timeScale = 1;
        go.GetComponent<GameOverScreen>().hidePause();
       


    }

}
