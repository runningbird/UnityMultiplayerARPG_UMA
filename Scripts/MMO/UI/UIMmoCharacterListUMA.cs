﻿using System.Collections.Generic;
using UnityEngine;
using LiteNetLibManager;
using MultiplayerARPG.GameData.Model.Playables;

namespace MultiplayerARPG.MMO
{
    public class UIMmoCharacterListUMA : UICharacterListUMA
    {
        protected override void LoadCharacters()
        {
            eventOnNotAbleToCreateCharacter.Invoke();
            MMOClientInstance.Singleton.RequestCharacters(OnRequestedCharacters);
        }

        private void OnRequestedCharacters(ResponseHandlerData responseHandler, AckResponseCode responseCode, ResponseCharactersMessage response)
        {
            // Clear character list
            CharacterSelectionManager.Clear();
            CharacterList.HideAll();
            // Unable buttons
            buttonStart.gameObject.SetActive(false);
            buttonDelete.gameObject.SetActive(false);
            // Remove all models
            characterModelContainer.RemoveChildren();
            _characterModelById.Clear();
            // Remove all cached data
            _playerCharacterDataById.Clear();
            // Proceed response
            List<PlayerCharacterData> selectableCharacters = new List<PlayerCharacterData>();
            if (!responseCode.ShowUnhandledResponseMessageDialog(response.message))
            {
                // Success, so set selectable characters by response's data
                selectableCharacters = response.characters;
            }
            // Show list of created characters
            for (int i = selectableCharacters.Count - 1; i >= 0; --i)
            {
                PlayerCharacterData selectableCharacter = selectableCharacters[i];
                if (selectableCharacter == null ||
                    !GameInstance.PlayerCharacterEntities.ContainsKey(selectableCharacter.EntityId) ||
                    !GameInstance.PlayerCharacters.ContainsKey(selectableCharacter.DataId))
                {
                    // If invalid entity id or data id, remove from selectable character list
                    selectableCharacters.RemoveAt(i);
                }
            }

            if (GameInstance.Singleton.maxCharacterSaves > 0 &&
                selectableCharacters.Count >= GameInstance.Singleton.maxCharacterSaves)
                eventOnNotAbleToCreateCharacter.Invoke();
            else
                eventOnAbleToCreateCharacter.Invoke();

            // Clear selected character data, will select first in list if available
            _selectedPlayerCharacterData = null;

            // Generate list entry by saved characters
            if (selectableCharacters.Count > 0)
            {
                selectableCharacters.Sort(new PlayerCharacterDataLastUpdateComparer().Desc());
                CharacterList.Generate(selectableCharacters, (index, characterData, ui) =>
                {
                    // Cache player character to dictionary, we will use it later
                    _playerCharacterDataById[characterData.Id] = characterData;
                    // Setup UIs
                    UICharacter uiCharacter = ui.GetComponent<UICharacter>();
                    uiCharacter.Data = characterData;
                    // Select trigger when add first entry so deactivate all models is okay because first model will active
                    PlayableCharacterModelUMA characterModel = (PlayableCharacterModelUMA)characterData.InstantiateModel(characterModelContainer);
                    if (characterModel != null)
                    {
                        _characterModelById[characterData.Id] = characterModel;
                        characterModel.SetEquipWeapons(characterData.EquipWeapons);
                        characterModel.SetEquipItems(characterData.EquipItems,characterModel.SelectableWeaponSets, characterData.EquipWeaponSet, characterModel.IsWeaponsSheathed);
                        characterModel.gameObject.SetActive(false);
                        CharacterSelectionManager.Add(uiCharacter);
                    }
                });
            }
            else
            {
                eventOnNoCharacter.Invoke();
            }
        }

        protected override void OnSelectCharacter(IPlayerCharacterData playerCharacterData)
        {
            if (buttonStart)
                buttonStart.gameObject.SetActive(true);
            if (buttonDelete)
                buttonDelete.gameObject.SetActive(true);
            characterModelContainer.SetChildrenActive(false);
            // Load selected character and also set selected player character data
            _playerCharacterDataById.TryGetValue(playerCharacterData.Id, out _selectedPlayerCharacterData);
            // Show selected character model
            _characterModelById.TryGetValue(playerCharacterData.Id, out _selectedModel);
            if (SelectedModel != null && SelectedModel is ICharacterModelUma)
            {
                // Setup Uma model and applies options
                ICharacterModelUma characterModelUMA = SelectedModel as ICharacterModelUma;
                UmaModel = characterModelUMA;
                SelectedModel.gameObject.SetActive(true);
                UmaModel.ApplyUmaAvatar(SelectedPlayerCharacterData.UmaAvatarData);
            }
        }

        public override void OnClickStart()
        {
            UICharacter selectedUI = CharacterSelectionManager.SelectedUI;
            if (selectedUI == null)
            {
                UISceneGlobal.Singleton.ShowMessageDialog(LanguageManager.GetText(UITextKeys.UI_LABEL_ERROR.ToString()), LanguageManager.GetText(UITextKeys.UI_ERROR_NO_CHOSEN_CHARACTER_TO_START.ToString()));
                Debug.LogWarning("Cannot start game, No chosen character");
                return;
            }
            // Load gameplay scene, we're going to manage maps in gameplay scene later
            // So we can add gameplay UI just once in gameplay scene
            IPlayerCharacterData playerCharacter = selectedUI.Data as IPlayerCharacterData;
            MMOClientInstance.Singleton.RequestSelectCharacter(playerCharacter.Id, OnRequestedSelectCharacter);
        }

        private void OnRequestedSelectCharacter(ResponseHandlerData responseHandler, AckResponseCode responseCode, ResponseSelectCharacterMessage response)
        {
            if (responseCode.ShowUnhandledResponseMessageDialog(response.message)) return;
            MMOClientInstance.Singleton.StartMapClient(response.sceneName, response.networkAddress, response.networkPort);
        }

        public override void OnClickDelete()
        {
            UICharacter selectedUI = CharacterSelectionManager.SelectedUI;
            if (selectedUI == null)
            {
                UISceneGlobal.Singleton.ShowMessageDialog(LanguageManager.GetText(UITextKeys.UI_LABEL_ERROR.ToString()), LanguageManager.GetText(UITextKeys.UI_ERROR_NO_CHOSEN_CHARACTER_TO_DELETE.ToString()));
                Debug.LogWarning("Cannot delete character, No chosen character");
                return;
            }

            IPlayerCharacterData playerCharacter = selectedUI.Data as IPlayerCharacterData;
            MMOClientInstance.Singleton.RequestDeleteCharacter(playerCharacter.Id, OnRequestedDeleteCharacter);
        }

        private void OnRequestedDeleteCharacter(ResponseHandlerData responseHandler, AckResponseCode responseCode, ResponseDeleteCharacterMessage response)
        {
            if (responseCode.ShowUnhandledResponseMessageDialog(response.message)) return;
            // Reload characters
            LoadCharacters();
        }
    }
}
