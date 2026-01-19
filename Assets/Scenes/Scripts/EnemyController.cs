using System.Collections;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();
        
        if (anim == null) 
            Debug.LogError("Waduh! Object " + gameObject.name + " belum punya komponen Animator!");
    }

    public void PlayAttack()
    {
        StartCoroutine(AttackRoutine());
    }

    public void PlayHit()
    {
        StartCoroutine(HitRoutine());
    }

    public void PlayDead()
    {
        StartCoroutine(DeadRoutine());
    }

    IEnumerator AttackRoutine()
    {
        if (anim != null)
        {
            anim.SetBool("isAttack", true);
            yield return new WaitForSeconds(0.5f); 
            
            anim.SetBool("isAttack", false);
        }
    }

    IEnumerator HitRoutine()
    {
        if (anim != null)
        {
            anim.SetBool("IsAttacked", true);
            yield return new WaitForSeconds(0.3f);
            anim.SetBool("IsAttacked", false);
        }
    }

    IEnumerator DeadRoutine()
    {
        if (anim != null)
        {
            anim.SetBool("isDead", true);
            yield return new WaitForSeconds(1f); 
            gameObject.SetActive(false);
            anim.SetBool("isDead", false);
        }
    }
}