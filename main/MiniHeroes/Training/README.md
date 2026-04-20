# MiniHeroes ML-Agents Guide

## Files

- `miniheroes_grunt_ppo.yaml`: PPO trainer config for the `MiniHeroesGrunt` behavior.

## Training Mode

The project enters training mode when Unity runs in batch mode or when you launch it with:

```powershell
Unity.exe -projectPath "C:\path\to\MiniHeroes" -miniheroes-train
```

In training mode:

- Login is bypassed.
- The player is driven by `TrainingPlayerBot`.
- The player respawns automatically.
- Grunts respawn automatically and keep producing episodes.
- Time scale is increased to speed up learning.

## Python Setup

Recommended versions for ML-Agents release 23 / Unity package 4.0.0:

- Python `3.10.12`
- `mlagents` `1.1.0`

Example:

```powershell
python -m venv .venv
.venv\Scripts\activate
python -m pip install --upgrade pip
python -m pip install mlagents==1.1.0
```

## Start Training

From this `Training` folder:

```powershell
mlagents-learn miniheroes_grunt_ppo.yaml --run-id miniheroes_grunt_v1 --time-scale 8
```

Then start the Unity project in training mode.

## One-Command Start

If your Python environment is already active and `mlagents-learn` is available in `PATH`, you can launch both the trainer and Unity with:

```powershell
.\Start-MiniHeroesTraining.ps1
```

Optional:

```powershell
.\Start-MiniHeroesTraining.ps1 -RunId miniheroes_test_01
```

Important:

- Close the project in Unity before running the script, or Unity may refuse to open a second instance.
- Activate your Python virtual environment first, otherwise the script will not find `mlagents-learn`.

## Generated Model

When training finishes, promote the latest model into the Unity project:

```powershell
.\Promote-LatestMiniHeroesModel.ps1
```

This copies the newest `.onnx` into:

```text
Assets\Resources\MLAgents\MiniHeroesGrunt.onnx
```

After Unity reimports it, the game will load it automatically in normal play mode through `MiniHeroesInferenceBootstrap`.

## Normal Play With Trained Enemies

1. Train a model.
2. Run `.\Promote-LatestMiniHeroesModel.ps1`
3. Wait for Unity to import the model asset.
4. Press Play normally.

If the model exists in `Resources/MLAgents/MiniHeroesGrunt`, grunts with behavior name `MiniHeroesGrunt` will switch to inference automatically.
